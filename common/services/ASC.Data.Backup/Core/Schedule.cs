﻿using System;
using System.Threading;

using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Data.Backup.EF.Model;
using ASC.Notify.Cron;

using Microsoft.Extensions.Options;

namespace ASC.Data.Backup
{
    public class Schedule
    {
        public TenantManager TenantManager { get; }
        public IOptionsMonitor<ILog> Options { get; }
        public TenantUtil TenantUtil { get; }
        public BackupHelper BackupHelper { get; }

        public Schedule(IOptionsMonitor<ILog> options, TenantManager tenantManager, TenantUtil tenantUtil, BackupHelper backupHelper)
        {
            Options = options;
            TenantManager = tenantManager;
            TenantUtil = tenantUtil;
            BackupHelper = backupHelper;
        }

        public bool IsToBeProcessed(BackupSchedule backupSchedule)
        {
            try
            {
                if (BackupHelper.ExceedsMaxAvailableSize(backupSchedule.TenantId)) throw new Exception("Backup file exceed " + backupSchedule.TenantId);

                var cron = new CronExpression(backupSchedule.Cron);
                var tenant = TenantManager.GetTenant(backupSchedule.TenantId);
                var tenantTimeZone = tenant.TimeZone;
                var culture = tenant.GetCulture();
                Thread.CurrentThread.CurrentCulture = culture;

                var lastBackupTime = backupSchedule.LastBackupTime.Equals(default)
                    ? DateTime.UtcNow.Date.AddSeconds(-1)
                    : TenantUtil.DateTimeFromUtc(tenantTimeZone, backupSchedule.LastBackupTime);

                var nextBackupTime = cron.GetTimeAfter(lastBackupTime);

                if (!nextBackupTime.HasValue) return false;
                var now = TenantUtil.DateTimeFromUtc(tenantTimeZone, DateTime.UtcNow);
                return nextBackupTime <= now;
            }
            catch (Exception e)
            {
                var log = Options.CurrentValue;
                log.Error("Schedule " + backupSchedule.TenantId, e);
                return false;
            }
        }
    }

    public static class ScheduleExtension
    {
        public static DIHelper AddScheduleService(this DIHelper services)
        {
            if (services.TryAddScoped<Schedule>())
            {

                return services
                    .AddTenantManagerService()
                    .AddTenantUtilService()
                    .AddBackupHelperService();
            }

            return services;
        }
    }
}
