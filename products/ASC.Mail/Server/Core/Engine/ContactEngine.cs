/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.ElasticSearch;
using ASC.Mail.Core.Dao.Expressions.Contact;
using ASC.Mail.Core.Entities;
using ASC.Mail.Enums;
using ASC.Mail.Models;
using ASC.Mail.Utils;
using ASC.Web.Core;
using Microsoft.Extensions.Options;

namespace ASC.Mail.Core.Engine
{
    public class ContactEngine
    {
        public int Tenant
        {
            get
            {
                return TenantManager.GetCurrentTenant().TenantId;
            }
        }

        public string User
        {
            get
            {
                return SecurityContext.CurrentAccount.ID.ToString();
            }
        }

        public ILog Log { get; private set; }
        public SecurityContext SecurityContext { get; }
        public TenantManager TenantManager { get; }
        public DaoFactory DaoFactory { get; }
        public IndexEngine IndexEngine { get; }
        public AccountEngine AccountEngine { get; }
        public ApiHelper ApiHelper { get; }
        public FactoryIndexer<MailContactWrapper> FactoryIndexer { get; }
        public FactoryIndexerHelper FactoryIndexerHelper { get; }
        public IServiceProvider ServiceProvider { get; }
        public WebItemSecurity WebItemSecurity { get; }

        public ContactEngine(
            SecurityContext securityContext,
            TenantManager tenantManager,
            DaoFactory daoFactory,
            IndexEngine indexEngine,
            AccountEngine accountEngine,
            ApiHelper apiHelper,
            FactoryIndexer<MailContactWrapper> factoryIndexer,
            FactoryIndexerHelper factoryIndexerHelper,
            WebItemSecurity webItemSecurity,
            IServiceProvider serviceProvider,
            IOptionsMonitor<ILog> option)
        {
            SecurityContext = securityContext;
            TenantManager = tenantManager;
            DaoFactory = daoFactory;
            IndexEngine = indexEngine;
            AccountEngine = accountEngine;
            ApiHelper = apiHelper;
            FactoryIndexer = factoryIndexer;
            FactoryIndexerHelper = factoryIndexerHelper;
            ServiceProvider = serviceProvider;
            WebItemSecurity = webItemSecurity;
            Log = option.Get("ASC.Mail.ContactEngine");
        }

        public List<ContactCard> GetContactCards(IContactsExp exp)
        {
            if (exp == null)
                throw new ArgumentNullException("exp");

            var list = DaoFactory.ContactCardDao.GetContactCards(exp);

            return list;
        }

        public int GetContactCardsCount(IContactsExp exp)
        {
            if (exp == null)
                throw new ArgumentNullException("exp");

            var count = DaoFactory.ContactCardDao.GetContactCardsCount(exp);

            return count;
        }

        public ContactCard GetContactCard(int id)
        {
            var contactCard = DaoFactory.ContactCardDao.GetContactCard(id);

            return contactCard;
        }

        public ContactCard SaveContactCard(ContactCard contactCard)
        {
            using (var tx = DaoFactory.BeginTransaction())
            {
                var contactId = DaoFactory.ContactDao.SaveContact(contactCard.ContactInfo);

                contactCard.ContactInfo.Id = contactId;

                foreach (var contactItem in contactCard.ContactItems)
                {
                    contactItem.ContactId = contactId;

                    var contactItemId = DaoFactory.ContactInfoDao.SaveContactInfo(contactItem);

                    contactItem.Id = contactItemId;
                }

                tx.Commit();
            }

            Log.Debug("IndexEngine->SaveContactCard()");

            IndexEngine.Add(contactCard.ToMailContactWrapper());

            return contactCard;
        }

        public ContactCard UpdateContactCard(ContactCard newContactCard)
        {
            var contactId = newContactCard.ContactInfo.Id;

            if (contactId < 0)
                throw new ArgumentException("Invalid contact id");

            var contactCard = GetContactCard(contactId);

            if (null == contactCard)
                throw new ArgumentException("Contact not found");

            var contactChanged = !contactCard.ContactInfo.Equals(newContactCard.ContactInfo);

            var newContactItems = newContactCard.ContactItems.Where(c => !contactCard.ContactItems.Contains(c)).ToList();

            var removedContactItems = contactCard.ContactItems.Where(c => !newContactCard.ContactItems.Contains(c)).ToList();

            if (!contactChanged && !newContactItems.Any() && !removedContactItems.Any())
                return contactCard;

            using (var tx = DaoFactory.BeginTransaction())
            {
                if (contactChanged)
                {
                    DaoFactory.ContactDao.SaveContact(newContactCard.ContactInfo);

                    contactCard.ContactInfo = newContactCard.ContactInfo;
                }

                if (newContactItems.Any())
                {
                    foreach (var contactItem in newContactItems)
                    {
                        contactItem.ContactId = contactId;

                        var contactItemId = DaoFactory.ContactInfoDao.SaveContactInfo(contactItem);

                        contactItem.Id = contactItemId;

                        contactCard.ContactItems.Add(contactItem);
                    }
                }

                if (removedContactItems.Any())
                {
                    foreach (var contactItem in removedContactItems)
                    {
                        DaoFactory.ContactInfoDao.RemoveContactInfo(contactItem.Id);

                        contactCard.ContactItems.Remove(contactItem);
                    }
                }

                tx.Commit();
            }

            Log.Debug("IndexEngine->UpdateContactCard()");

            IndexEngine.Update(new List<MailContactWrapper> { contactCard.ToMailContactWrapper() });

            return contactCard;
        }

        public void RemoveContacts(List<int> ids)
        {
            if (!ids.Any())
                throw new ArgumentNullException("ids");

            using (var tx = DaoFactory.BeginTransaction())
            {
                DaoFactory.ContactDao.RemoveContacts(ids);

                DaoFactory.ContactInfoDao.RemoveByContactIds(ids);

                tx.Commit();
            }

            Log.Debug("IndexEngine->RemoveContacts()");

            IndexEngine.RemoveContacts(ids, Tenant, new Guid(User));
        }

        /// <summary>
        /// Search emails in Accounts, Mail, CRM, Peaople Contact System
        /// </summary>
        /// <param name="tenant">Tenant id</param>
        /// <param name="userName">User id</param>
        /// <param name="term">Search word</param>
        /// <param name="maxCountPerSystem">limit result per Contact System</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="httpContextScheme"></param>
        /// <returns></returns>
        public List<string> SearchEmails(int tenant, string userName, string term, int maxCountPerSystem, string httpContextScheme, int timeout = -1)
        {
            var equality = new ContactEqualityComparer();
            var contacts = new List<string>();
            var userGuid = new Guid(userName);

            var watch = new Stopwatch();

            watch.Start();

            var taskList = new List<Task<List<string>>>()
            {
                Task.Run(() =>
                {
                    TenantManager.SetCurrentTenant(tenant);
                    SecurityContext.AuthenticateMe(userGuid);

                    var exp = new FullFilterContactsExp(tenant, userName, DaoFactory.MailDb, FactoryIndexer, FactoryIndexerHelper, ServiceProvider, 
                        term, infoType: ContactInfoType.Email, orderAsc: true, limit: maxCountPerSystem);

                    var contactCards = GetContactCards(exp);

                    return (from contactCard in contactCards
                        from contactItem in contactCard.ContactItems
                        select
                            string.IsNullOrEmpty(contactCard.ContactInfo.ContactName)
                                ? contactItem.Data
                                : MailUtil.CreateFullEmail(contactCard.ContactInfo.ContactName, contactItem.Data))
                        .ToList();
                }),

                Task.Run(() =>
                {
                    TenantManager.SetCurrentTenant(tenant);
                    SecurityContext.AuthenticateMe(userGuid);

                    return AccountEngine.SearchAccountEmails(term);
                }),

                Task.Run(() =>
                {
                    TenantManager.SetCurrentTenant(tenant);
                    SecurityContext.AuthenticateMe(userGuid);

                    return WebItemSecurity.IsAvailableForMe(WebItemManager.CRMProductID)
                        ? ApiHelper.SearchCrmEmails(term, maxCountPerSystem)
                        : new List<string>();
                }),

                Task.Run(() =>
                {
                    TenantManager.SetCurrentTenant(tenant);
                    SecurityContext.AuthenticateMe(userGuid);

                    return WebItemSecurity.IsAvailableForMe(WebItemManager.PeopleProductID)
                        ? ApiHelper.SearchPeopleEmails(term, 0, maxCountPerSystem)
                        : new List<string>();
                })
            };

            try
            {
                var taskArray = taskList.ToArray<Task>();

                Task.WaitAll(taskArray, timeout);

                watch.Stop();
            }
            catch (AggregateException e)
            {
                watch.Stop();

                var errorText =
                    new StringBuilder("SearchEmails: \nThe following exceptions have been thrown by WaitAll():");

                foreach (var t in e.InnerExceptions)
                {
                    errorText
                        .AppendFormat("\n-------------------------------------------------\n{0}", t);
                }

                Log.Error(errorText.ToString());
            }

            contacts =
                taskList.Aggregate(contacts,
                    (current, task) => !task.IsFaulted
                                       && task.IsCompleted
                                       && !task.IsCanceled
                        ? current.Concat(task.Result).ToList()
                        : current)
                    .Distinct(equality)
                    .ToList();

            Log.DebugFormat("SearchEmails (term = '{0}'): {1} sec / {2} items", term, watch.Elapsed.TotalSeconds, contacts.Count);

            return contacts;
        }

        public class ContactEqualityComparer : IEqualityComparer<string>
        {
            public bool Equals(string contact1, string contact2)
            {
                if (contact1 == null && contact2 == null)
                    return true;

                if (contact1 == null || contact2 == null)
                    return false;

                var contact1Parts = contact1.Split('<');
                var contact2Parts = contact2.Split('<');

                return contact1Parts.Last().Replace(">", "") == contact2Parts.Last().Replace(">", "");
            }

            public int GetHashCode(string str)
            {
                var strParts = str.Split('<');
                return strParts.Last().Replace(">", "").GetHashCode();
            }
        }
    }

    public static class ContactEngineExtension
    {
        public static DIHelper AddContactEngineService(this DIHelper services)
        {
            services.TryAddScoped<ContactEngine>();

            services.AddSecurityContextService()
                .AddTenantManagerService()
                .AddDaoFactoryService()
                .AddIndexEngineService()
                .AddAccountEngineService()
                .AddApiHelperService()
                .AddFactoryIndexerService<MailContactWrapper>()
                .AddFactoryIndexerHelperService()
                .AddWebItemSecurity();

            return services;
        }
    }
}