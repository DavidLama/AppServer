﻿/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
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
using System.Linq;

using ASC.Common;
using ASC.Common.Caching;
using ASC.Core.Common.EF.Model;

using Microsoft.Extensions.Options;

namespace ASC.Core.Common.Notify.Telegram
{
    class ConfigureCachedTelegramDao : IConfigureNamedOptions<CachedTelegramDao>
    {
        private IOptionsSnapshot<TelegramDao> Service { get; }

        public ConfigureCachedTelegramDao(IOptionsSnapshot<TelegramDao> service)
        {
            Service = service;
        }

        public void Configure(string name, CachedTelegramDao options)
        {
            Configure(options);
            options.tgDao = Service.Get(name);
        }

        public void Configure(CachedTelegramDao options)
        {
            options.tgDao = Service.Value;
            options.cache = AscCache.Memory;
            options.Expiration = TimeSpan.FromMinutes(20);

            options.PairKeyFormat = "tgUser:{0}:{1}";
            options.SingleKeyFormat = "tgUser:{0}";
        }
    }

    public class CachedTelegramDao
    {
        public TelegramDao tgDao { get; set; }
        public ICache cache { get; set; }
        public TimeSpan Expiration { get; set; }

        public string PairKeyFormat { get; set; }
        public string SingleKeyFormat { get; set; }


        public void Delete(Guid userId, int tenantId)
        {
            cache.Remove(string.Format(PairKeyFormat, userId, tenantId));
            tgDao.Delete(userId, tenantId);
        }

        public void Delete(int telegramId)
        {
            cache.Remove(string.Format(SingleKeyFormat, telegramId));
            tgDao.Delete(telegramId);
        }

        public TelegramUser GetUser(Guid userId, int tenantId)
        {
            var key = string.Format(PairKeyFormat, userId, tenantId);

            var user = cache.Get<TelegramUser>(key);
            if (user != null) return user;

            user = tgDao.GetUser(userId, tenantId);
            if (user != null) cache.Insert(key, user, Expiration);
            return user;
        }

        public List<TelegramUser> GetUser(int telegramId)
        {
            var key = string.Format(SingleKeyFormat, telegramId);

            var users = cache.Get<List<TelegramUser>>(key);
            if (users != null) return users;

            users = tgDao.GetUser(telegramId);
            if (users.Any()) cache.Insert(key, users, Expiration);
            return users;
        }

        public void RegisterUser(Guid userId, int tenantId, int telegramId)
        {
            tgDao.RegisterUser(userId, tenantId, telegramId);

            var key = string.Format(PairKeyFormat, userId, tenantId);
            cache.Insert(key, new TelegramUser { PortalUserId = userId, TenantId = tenantId, TelegramUserId = telegramId }, Expiration);
        }
    }

    public static class CachedTelegramDaoExtension
    {
        public static DIHelper AddTenantService(this DIHelper services)
        {
            if (services.TryAddScoped<TelegramDao>())
            {
                services.TryAddScoped<CachedTelegramDao>();

                services.TryAddScoped<IConfigureOptions<TelegramDao>, ConfigureTelegramDaoService>();
                services.TryAddScoped<IConfigureOptions<CachedTelegramDao>, ConfigureCachedTelegramDao>();

                services.TryAddSingleton(typeof(ICacheNotify<>), typeof(KafkaCache<>));

                return services;
            }

            return services;
        }
    }
}
