﻿using System.Linq;

using ASC.Common;
using ASC.Common.Caching;
using ASC.Core;
using ASC.Notify.Model;
using ASC.Notify.Patterns;
using ASC.Notify.Recipients;
using ASC.Web.Studio.Core.Notify;

namespace ASC.Web.Core.Notify
{
    public class StudioNotifyServiceHelper
    {
        private ICacheNotify<NotifyItem> Cache { get; }
        private StudioNotifyHelper StudioNotifyHelper { get; }
        private AuthContext AuthContext { get; }
        private TenantManager TenantManager { get; }

        public StudioNotifyServiceHelper(StudioNotifyHelper studioNotifyHelper, AuthContext authContext, TenantManager tenantManager, ICacheNotify<NotifyItem> cache)
        {
            StudioNotifyHelper = studioNotifyHelper;
            AuthContext = authContext;
            TenantManager = tenantManager;
            Cache = cache;
        }

        public void SendNoticeToAsync(INotifyAction action, IRecipient[] recipients, string[] senderNames, params ITagValue[] args)
        {
            SendNoticeToAsync(action, null, recipients, senderNames, false, args);
        }

        public void SendNoticeToAsync(INotifyAction action, string objectID, IRecipient[] recipients, string[] senderNames, params ITagValue[] args)
        {
            SendNoticeToAsync(action, objectID, recipients, senderNames, false, args);
        }

        public void SendNoticeToAsync(INotifyAction action, string objectID, IRecipient[] recipients, params ITagValue[] args)
        {
            SendNoticeToAsync(action, objectID, recipients, null, false, args);
        }

        public void SendNoticeToAsync(INotifyAction action, string objectID, IRecipient[] recipients, bool checkSubscription, params ITagValue[] args)
        {
            SendNoticeToAsync(action, objectID, recipients, null, checkSubscription, args);
        }

        public void SendNoticeAsync(INotifyAction action, string objectID, IRecipient recipient, params ITagValue[] args)
        {
            SendNoticeToAsync(action, objectID, new[] { recipient }, null, false, args);
        }

        public void SendNoticeAsync(INotifyAction action, string objectID, params ITagValue[] args)
        {
            var subscriptionSource = StudioNotifyHelper.NotifySource.GetSubscriptionProvider();
            var recipients = subscriptionSource.GetRecipients(action, objectID);

            SendNoticeToAsync(action, objectID, recipients, null, false, args);
        }

        public void SendNoticeAsync(INotifyAction action, string objectID, IRecipient recipient, bool checkSubscription, params ITagValue[] args)
        {
            SendNoticeToAsync(action, objectID, new[] { recipient }, null, checkSubscription, args);
        }

        public void SendNoticeToAsync(INotifyAction action, string objectID, IRecipient[] recipients, string[] senderNames, bool checkSubsciption, params ITagValue[] args)
        {
            var item = new NotifyItem
            {
                TenantId = TenantManager.GetCurrentTenant().TenantId,
                UserId = AuthContext.CurrentAccount.ID.ToString(),
                Action = (NotifyAction)action,
                CheckSubsciption = checkSubsciption
            };

            if (objectID != null)
            {
                item.ObjectID = objectID;
            }

            if (recipients != null)
            {
                foreach (var r in recipients)
                {
                    var recipient = new Recipient { ID = r.ID, Name = r.Name };
                    if (r is IDirectRecipient d)
                    {
                        recipient.Addresses.AddRange(d.Addresses);
                        recipient.CheckActivation = d.CheckActivation;
                    }

                    if (r is IRecipientsGroup g)
                    {
                        recipient.IsGroup = true;
                    }

                    item.Recipients.Add(recipient);
                }
            }

            if (senderNames != null)
            {
                item.SenderNames.AddRange(senderNames);
            }

            if (args != null)
            {
                item.Tags.AddRange(args.Select(r => new Tag { Tag_ = r.Tag, Value = r.Value.ToString() }));
            }

            Cache.Publish(item, CacheNotifyAction.Any);
        }
    }

    public static class StudioNotifyServiceHelperExtension
    {
        public static DIHelper AddStudioNotifyServiceHelper(this DIHelper services)
        {
            if (services.TryAddScoped<StudioNotifyServiceHelper>())
            {
                services.TryAddSingleton(typeof(ICacheNotify<>), typeof(KafkaCache<>));

                return services
                    .AddAuthContextService()
                    .AddStudioNotifyHelperService()
                    .AddTenantManagerService();
            }

            return services;
        }
    }
}