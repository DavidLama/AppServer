/*
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
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using ASC.Common;
using ASC.Core;
using ASC.Core.Users;
using ASC.Files.Core;
using ASC.Files.Core.Data;
using ASC.Files.Core.Security;
using ASC.Files.Resources;
using ASC.MessagingSystem;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Helpers;
using ASC.Web.Studio.Core;
using ASC.Web.Studio.UserControls.Statistics;
using ASC.Web.Studio.Utility;

using Microsoft.Extensions.DependencyInjection;

namespace ASC.Web.Files.Utils
{
    public class FileUploader
    {
        public FilesSettingsHelper FilesSettingsHelper { get; }
        public FileUtility FileUtility { get; }
        public UserManager UserManager { get; }
        public TenantManager TenantManager { get; }
        public AuthContext AuthContext { get; }
        public SetupInfo SetupInfo { get; }
        public TenantExtra TenantExtra { get; }
        public TenantStatisticsProvider TenantStatisticsProvider { get; }
        public FileMarker FileMarker { get; }
        public FileConverter FileConverter { get; }
        public IDaoFactory DaoFactory { get; }
        public Global Global { get; }
        public FilesLinkUtility FilesLinkUtility { get; }
        public FilesMessageService FilesMessageService { get; }
        public FileSecurity FileSecurity { get; }
        public EntryManager EntryManager { get; }
        public IServiceProvider ServiceProvider { get; }
        public ChunkedUploadSessionHolder ChunkedUploadSessionHolder { get; }

        public FileUploader(
            FilesSettingsHelper filesSettingsHelper,
            FileUtility fileUtility,
            UserManager userManager,
            TenantManager tenantManager,
            AuthContext authContext,
            SetupInfo setupInfo,
            TenantExtra tenantExtra,
            TenantStatisticsProvider tenantStatisticsProvider,
            FileMarker fileMarker,
            FileConverter fileConverter,
            IDaoFactory daoFactory,
            Global global,
            FilesLinkUtility filesLinkUtility,
            FilesMessageService filesMessageService,
            FileSecurity fileSecurity,
            EntryManager entryManager,
            IServiceProvider serviceProvider,
            ChunkedUploadSessionHolder chunkedUploadSessionHolder)
        {
            FilesSettingsHelper = filesSettingsHelper;
            FileUtility = fileUtility;
            UserManager = userManager;
            TenantManager = tenantManager;
            AuthContext = authContext;
            SetupInfo = setupInfo;
            TenantExtra = tenantExtra;
            TenantStatisticsProvider = tenantStatisticsProvider;
            FileMarker = fileMarker;
            FileConverter = fileConverter;
            DaoFactory = daoFactory;
            Global = global;
            FilesLinkUtility = filesLinkUtility;
            FilesMessageService = filesMessageService;
            FileSecurity = fileSecurity;
            EntryManager = entryManager;
            ServiceProvider = serviceProvider;
            ChunkedUploadSessionHolder = chunkedUploadSessionHolder;
        }

        public async Task<File<T>> Exec<T>(T folderId, string title, long contentLength, Stream data)
        {
            return await Exec(folderId, title, contentLength, data, !FilesSettingsHelper.UpdateIfExist);
        }

        public async Task<File<T>> Exec<T>(T folderId, string title, long contentLength, Stream data, bool createNewIfExist, bool deleteConvertStatus = true)
        {
            if (contentLength <= 0)
                throw new Exception(FilesCommonResource.ErrorMassage_EmptyFile);

            var file = await VerifyFileUpload(folderId, title, contentLength, !createNewIfExist);

            var dao = DaoFactory.GetFileDao<T>();
            file = await dao.SaveFile(file, data);

            await FileMarker.MarkAsNew(file);

            if (FileConverter.EnableAsUploaded && FileConverter.MustConvert(file))
                await FileConverter.ExecAsync(file, deleteConvertStatus);

            return file;
        }

        public async Task<File<T>> VerifyFileUpload<T>(T folderId, string fileName, bool updateIfExists, string relativePath = null)
        {
            fileName = Global.ReplaceInvalidCharsAndTruncate(fileName);

            if (Global.EnableUploadFilter && !FileUtility.ExtsUploadable.Contains(FileUtility.GetFileExtension(fileName)))
                throw new NotSupportedException(FilesCommonResource.ErrorMassage_NotSupportedFormat);

            folderId = await GetFolderId(folderId, string.IsNullOrEmpty(relativePath) ? null : relativePath.Split('/').ToList());

            var fileDao = DaoFactory.GetFileDao<T>();
            var file = await fileDao.GetFile(folderId, fileName);

            if (updateIfExists && await CanEdit(file))
            {
                file.Title = fileName;
                file.ConvertedType = null;
                file.Comment = FilesCommonResource.CommentUpload;
                file.Version++;
                file.VersionGroup++;
                file.Encrypted = false;

                return file;
            }

            var newFile = ServiceProvider.GetService<File<T>>();
            newFile.FolderID = folderId;
            newFile.Title = fileName;
            return newFile;
        }

        public async Task<File<T>> VerifyFileUpload<T>(T folderId, string fileName, long fileSize, bool updateIfExists)
        {
            if (fileSize <= 0)
                throw new Exception(FilesCommonResource.ErrorMassage_EmptyFile);

            var maxUploadSize = GetMaxFileSize(folderId);

            if (fileSize > maxUploadSize)
                throw FileSizeComment.GetFileSizeException(maxUploadSize);

            var file = await VerifyFileUpload(folderId, fileName, updateIfExists);
            file.ContentLength = fileSize;
            return file;
        }

        private async Task<bool> CanEdit<T>(File<T> file)
        {
            return file != null
                   && await FileSecurity.CanEdit(file)
                   && !UserManager.GetUsers(AuthContext.CurrentAccount.ID).IsVisitor(UserManager)
                   && !EntryManager.FileLockedForMe(file.ID)
                   && !FileTracker.IsEditing(file.ID)
                   && file.RootFolderType != FolderType.TRASH
                   && !file.Encrypted;
        }

        private async Task<T> GetFolderId<T>(T folderId, IList<string> relativePath)
        {
            var folderDao = DaoFactory.GetFolderDao<T>();
            var folder = await folderDao.GetFolder(folderId);

            if (folder == null)
                throw new DirectoryNotFoundException(FilesCommonResource.ErrorMassage_FolderNotFound);

            if (!await FileSecurity.CanCreate(folder))
                throw new SecurityException(FilesCommonResource.ErrorMassage_SecurityException_Create);

            if (relativePath != null && relativePath.Any())
            {
                var subFolderTitle = Global.ReplaceInvalidCharsAndTruncate(relativePath.FirstOrDefault());

                if (!string.IsNullOrEmpty(subFolderTitle))
                {
                    folder = await folderDao.GetFolder(subFolderTitle, folder.ID);

                    if (folder == null)
                    {
                        var newFolder = ServiceProvider.GetService<Folder<T>>();
                        newFolder.Title = subFolderTitle;
                        newFolder.ParentFolderID = folderId;

                        folderId = await folderDao.SaveFolder(newFolder);

                        folder = await folderDao.GetFolder(folderId);
                        FilesMessageService.Send(folder, MessageAction.FolderCreated, folder.Title);
                    }

                    folderId = folder.ID;

                    relativePath.RemoveAt(0);
                    folderId = await GetFolderId(folderId, relativePath);
                }
            }

            return folderId;
        }

        #region chunked upload

        public async Task<File<T>> VerifyChunkedUpload<T>(T folderId, string fileName, long fileSize, bool updateIfExists, string relativePath = null)
        {
            var maxUploadSize = GetMaxFileSize(folderId, true);

            if (fileSize > maxUploadSize)
                throw FileSizeComment.GetFileSizeException(maxUploadSize);

            var file = await VerifyFileUpload(folderId, fileName, updateIfExists, relativePath);
            file.ContentLength = fileSize;

            return file;
        }

        public async Task<ChunkedUploadSession<T>> InitiateUpload<T>(T folderId, T fileId, string fileName, long contentLength, bool encrypted)
        {
            var file = ServiceProvider.GetService<File<T>>();
            file.ID = fileId;
            file.FolderID = folderId;
            file.Title = fileName;
            file.ContentLength = contentLength;

            var dao = DaoFactory.GetFileDao<T>();
            var uploadSession = await dao.CreateUploadSession(file, contentLength);

            uploadSession.Expired = uploadSession.Created + ChunkedUploadSessionHolder.SlidingExpiration;
            uploadSession.Location = FilesLinkUtility.GetUploadChunkLocationUrl(uploadSession.Id);
            uploadSession.TenantId = TenantManager.GetCurrentTenant().TenantId;
            uploadSession.UserId = AuthContext.CurrentAccount.ID;
            uploadSession.FolderId = folderId;
            uploadSession.CultureName = Thread.CurrentThread.CurrentUICulture.Name;
            uploadSession.Encrypted = encrypted;

            ChunkedUploadSessionHolder.StoreSession(uploadSession);

            return uploadSession;
        }

        public async Task<ChunkedUploadSession<T>> UploadChunk<T>(string uploadId, Stream stream, long chunkLength)
        {
            var uploadSession = ChunkedUploadSessionHolder.GetSession<T>(uploadId);
            uploadSession.Expired = DateTime.UtcNow + ChunkedUploadSessionHolder.SlidingExpiration;

            if (chunkLength <= 0)
            {
                throw new Exception(FilesCommonResource.ErrorMassage_EmptyFile);
            }

            if (chunkLength > SetupInfo.ChunkUploadSize)
            {
                throw FileSizeComment.GetFileSizeException(SetupInfo.MaxUploadSize(TenantExtra, TenantStatisticsProvider));
            }

            var maxUploadSize = GetMaxFileSize(uploadSession.FolderId, uploadSession.BytesTotal > 0);

            if (uploadSession.BytesUploaded + chunkLength > maxUploadSize)
            {
                AbortUpload(uploadSession);
                throw FileSizeComment.GetFileSizeException(maxUploadSize);
            }

            var dao = DaoFactory.GetFileDao<T>();
            await dao.UploadChunk(uploadSession, stream, chunkLength);

            if (uploadSession.BytesUploaded == uploadSession.BytesTotal)
            {
                await FileMarker.MarkAsNew(uploadSession.File);
                ChunkedUploadSessionHolder.RemoveSession(uploadSession);
            }
            else
            {
                ChunkedUploadSessionHolder.StoreSession(uploadSession);
            }

            return uploadSession;
        }

        public void AbortUpload<T>(string uploadId)
        {
            AbortUpload(ChunkedUploadSessionHolder.GetSession<T>(uploadId));
        }

        private void AbortUpload<T>(ChunkedUploadSession<T> uploadSession)
        {
            DaoFactory.GetFileDao<T>().AbortUploadSession(uploadSession);

            ChunkedUploadSessionHolder.RemoveSession(uploadSession);
        }

        private long GetMaxFileSize<T>(T folderId, bool chunkedUpload = false)
        {
            var folderDao = DaoFactory.GetFolderDao<T>();
            return folderDao.GetMaxUploadSize(folderId, chunkedUpload);
        }

        #endregion
    }

    public static class FileUploaderExtention
    {
        public static DIHelper AddFileUploaderService(this DIHelper services)
        {
            services.TryAddScoped<FileUploader>();

            return services
                .AddChunkedUploadSessionHolderService()
                .AddEntryManagerService()
                .AddFileSecurityService()
                .AddFilesLinkUtilityService()
                .AddFilesMessageService()
                .AddGlobalService()
                .AddDaoFactoryService()
                .AddFileConverterService()
                .AddFileMarkerService()
                .AddTenantStatisticsProviderService()
                .AddTenantExtraService()
                .AddUserManagerService()
                .AddTenantManagerService()
                .AddAuthContextService()
                .AddSetupInfo()
                .AddFileUtilityService()
                .AddFilesSettingsHelperService()
                ;
        }
    }
}