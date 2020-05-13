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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ASC.Common.Security.Authentication;
using ASC.Common.Threading;
using ASC.Core.Tenants;
using ASC.Data.Storage;
using ASC.Files.Core;
using ASC.Files.Resources;
using ASC.MessagingSystem;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Helpers;
using ASC.Web.Files.Utils;
using ASC.Web.Studio.Core;

using Ionic.Zip;

using Microsoft.Extensions.DependencyInjection;

namespace ASC.Web.Files.Services.WCFService.FileOperations
{
    internal class FileDownloadOperationData<T> : FileOperationData<T>
    {
        public Dictionary<T, string> FilesDownload { get; }
        public Dictionary<string, string> Headers { get; }

        public FileDownloadOperationData(Dictionary<T, string> folders, Dictionary<T, string> files, Tenant tenant, Dictionary<string, string> headers, bool holdResult = true)
            : base(folders.Select(f => f.Key).ToList(), files.Select(f => f.Key).ToList(), tenant, holdResult)
        {
            FilesDownload = files;
            Headers = headers;
        }
    }

    class FileDownloadOperation : ComposeFileOperation<FileDownloadOperationData<string>, FileDownloadOperationData<int>>
    {
        public FileDownloadOperation(IServiceProvider serviceProvider, FileOperation<FileDownloadOperationData<string>, string> f1, FileOperation<FileDownloadOperationData<int>, int> f2)
            : base(serviceProvider, f1, f2)
        {
        }

        public override FileOperationType OperationType
        {
            get { return FileOperationType.Download; }
        }

        public override void RunJob(DistributedTask _, CancellationToken cancellationToken)
        {
            base.RunJob(_, cancellationToken);

            using var scope = ThirdPartyOperation.CreateScope();
            var globalStore = scope.ServiceProvider.GetService<GlobalStore>();
            var filesLinkUtility = scope.ServiceProvider.GetService<FilesLinkUtility>();

            using var stream = TempStream.Create();
            using (var zip = new ZipOutputStream(stream, true)
            {
                CompressionLevel = Ionic.Zlib.CompressionLevel.Level3,
                AlternateEncodingUsage = ZipOption.AsNecessary,
                AlternateEncoding = Encoding.UTF8,
            })
            {
                (ThirdPartyOperation as FileDownloadOperation<string>).CompressToZip(zip, stream, scope).Wait();
                (DaoOperation as FileDownloadOperation<int>).CompressToZip(zip, stream, scope).Wait();
            }

            if (stream != null)
            {
                stream.Position = 0;
                const string fileName = FileConstant.DownloadTitle + ".zip";
                var store = globalStore.GetStore();
                store.Save(
                    FileConstant.StorageDomainTmp,
                    string.Format(@"{0}\{1}", ((IAccount)Thread.CurrentPrincipal.Identity).ID, fileName),
                    stream,
                    "application/zip",
                    "attachment; filename=\"" + fileName + "\"");
                Status = string.Format("{0}?{1}=bulk", filesLinkUtility.FileHandlerPath, FilesLinkUtility.Action);
            }

            FillDistributedTask();
            TaskInfo.PublishChanges();
        }
    }

    class FileDownloadOperation<T> : FileOperation<FileDownloadOperationData<T>, T>
    {
        private readonly Dictionary<T, string> files;
        private readonly Dictionary<string, string> headers;
        ItemNameValueCollection<T> entriesPathId;
        public override FileOperationType OperationType
        {
            get { return FileOperationType.Download; }
        }

        public bool Compress { get; }

        public FileDownloadOperation(IServiceProvider serviceProvider, FileDownloadOperationData<T> fileDownloadOperationData, bool compress = true)
            : base(serviceProvider, fileDownloadOperationData)
        {
            files = fileDownloadOperationData.FilesDownload;
            headers = fileDownloadOperationData.Headers;
            Compress = compress;
        }


        protected override async Task Do(IServiceScope scope)
        {
            if (!Compress && !Files.Any() && !Folders.Any()) return;

            entriesPathId = await GetEntriesPathId(scope);
            if (entriesPathId == null || entriesPathId.Count == 0)
            {
                if (Files.Count > 0)
                {
                    throw new FileNotFoundException(FilesCommonResource.ErrorMassage_FileNotFound);
                }

                throw new DirectoryNotFoundException(FilesCommonResource.ErrorMassage_FolderNotFound);
            }

            var globalStore = scope.ServiceProvider.GetService<GlobalStore>();
            var filesLinkUtility = scope.ServiceProvider.GetService<FilesLinkUtility>();

            ReplaceLongPath(entriesPathId);

            if (Compress)
            {
                using var stream = TempStream.Create();
                using var zip = new ZipOutputStream(stream, true)
                {
                    CompressionLevel = Ionic.Zlib.CompressionLevel.Level3,
                    AlternateEncodingUsage = ZipOption.AsNecessary,
                    AlternateEncoding = Encoding.UTF8
                };

                await CompressToZip(zip, stream, scope);

                if (stream != null)
                {
                    stream.Position = 0;
                    const string fileName = FileConstant.DownloadTitle + ".zip";
                    var store = globalStore.GetStore();
                    store.Save(
                        FileConstant.StorageDomainTmp,
                        string.Format(@"{0}\{1}", ((IAccount)Thread.CurrentPrincipal.Identity).ID, fileName),
                        stream,
                        "application/zip",
                        "attachment; filename=\"" + fileName + "\"");
                    Status = string.Format("{0}?{1}=bulk", filesLinkUtility.FileHandlerPath, FilesLinkUtility.Action);
                }
            }
        }

        private async Task<ItemNameValueCollection<T>> ExecPathFromFile(IServiceScope scope, File<T> file, string path)
        {
            var fileMarker = scope.ServiceProvider.GetService<FileMarker>();
            await fileMarker.RemoveMarkAsNew(file);

            var title = file.Title;

            if (files.ContainsKey(file.ID))
            {
                var convertToExt = files[file.ID];

                if (!string.IsNullOrEmpty(convertToExt))
                {
                    title = FileUtility.ReplaceFileExtension(title, convertToExt);
                }
            }

            var entriesPathId = new ItemNameValueCollection<T>();
            entriesPathId.Add(path + title, file.ID);

            return entriesPathId;
        }

        private async Task<ItemNameValueCollection<T>> GetEntriesPathId(IServiceScope scope)
        {
            var fileMarker = scope.ServiceProvider.GetService<FileMarker>();
            var entriesPathId = new ItemNameValueCollection<T>();
            if (0 < Files.Count)
            {
                var files = await FileDao.GetFiles(Files.ToArray());
                files = (await FilesSecurity.FilterRead(files)).ToList();
                files.ForEach(async file => entriesPathId.Add(await ExecPathFromFile(scope, file, string.Empty)));
            }
            if (0 < Folders.Count)
            {
                (await FilesSecurity.FilterRead(await FolderDao.GetFolders(Files.ToArray())))
                    .Cast<FileEntry<T>>()
                    .ToList()
                    .ForEach(async folder => await fileMarker.RemoveMarkAsNew(folder));

                var filesInFolder = await GetFilesInFolders(scope, Folders, string.Empty);
                entriesPathId.Add(filesInFolder);
            }
            return entriesPathId;
        }

        private async Task<ItemNameValueCollection<T>> GetFilesInFolders(IServiceScope scope, IEnumerable<T> folderIds, string path)
        {
            var fileMarker = scope.ServiceProvider.GetService<FileMarker>();

            CancellationToken.ThrowIfCancellationRequested();

            var entriesPathId = new ItemNameValueCollection<T>();
            foreach (var folderId in folderIds)
            {
                CancellationToken.ThrowIfCancellationRequested();

                var folder = await FolderDao.GetFolder(folderId);
                if (folder == null || !await FilesSecurity.CanRead(folder)) continue;
                var folderPath = path + folder.Title + "/";

                var files = await FileDao.GetFiles(folder.ID, null, FilterType.None, false, Guid.Empty, string.Empty, true);
                files = (await FilesSecurity.FilterRead(files)).ToList();
                files.ForEach(async file => entriesPathId.Add(await ExecPathFromFile(scope, file, folderPath)));

                await fileMarker.RemoveMarkAsNew(folder);

                var nestedFolders = await FolderDao.GetFolders(folder.ID);
                nestedFolders = (await FilesSecurity.FilterRead(nestedFolders)).ToList();
                if (files.Count == 0 && nestedFolders.Count == 0)
                {
                    entriesPathId.Add(folderPath, default(T));
                }

                var filesInFolder = await GetFilesInFolders(scope, nestedFolders.ConvertAll(f => f.ID), folderPath);
                entriesPathId.Add(filesInFolder);
            }
            return entriesPathId;
        }

        internal async Task CompressToZip(ZipOutputStream zip, Stream stream, IServiceScope scope)
        {
            if (entriesPathId == null) return;
            var setupInfo = scope.ServiceProvider.GetService<SetupInfo>();
            var fileConverter = scope.ServiceProvider.GetService<FileConverter>();
            var filesMessageService = scope.ServiceProvider.GetService<FilesMessageService>();
            var FileDao = scope.ServiceProvider.GetService<IFileDao<T>>();

            foreach (var path in entriesPathId.AllKeys)
            {
                var counter = 0;
                foreach (var entryId in entriesPathId[path])
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        zip.Dispose();
                        stream.Dispose();
                        CancellationToken.ThrowIfCancellationRequested();
                    }

                    var newtitle = path;

                    File<T> file = null;
                    var convertToExt = string.Empty;

                    if (!entryId.Equals(default(T)))
                    {
                        FileDao.InvalidateCache(entryId);
                        file = await FileDao.GetFile(entryId);

                        if (file == null)
                        {
                            Error = FilesCommonResource.ErrorMassage_FileNotFound;
                            continue;
                        }

                        if (file.ContentLength > setupInfo.AvailableFileSize)
                        {
                            Error = string.Format(FilesCommonResource.ErrorMassage_FileSizeZip, FileSizeComment.FilesSizeToString(setupInfo.AvailableFileSize));
                            continue;
                        }

                        if (files.ContainsKey(file.ID))
                        {
                            convertToExt = files[file.ID];
                            if (!string.IsNullOrEmpty(convertToExt))
                            {
                                newtitle = FileUtility.ReplaceFileExtension(path, convertToExt);
                            }
                        }
                    }

                    if (0 < counter)
                    {
                        var suffix = " (" + counter + ")";

                        if (!entryId.Equals(default(T)))
                        {
                            newtitle = 0 < newtitle.IndexOf('.') ? newtitle.Insert(newtitle.LastIndexOf('.'), suffix) : newtitle + suffix;
                        }
                        else
                        {
                            break;
                        }
                    }

                    zip.PutNextEntry(newtitle);

                    if (!entryId.Equals(default(T)) && file != null)
                    {
                        try
                        {
                            if (fileConverter.EnableConvert(file, convertToExt))
                            {
                                //Take from converter
                                using var readStream = fileConverter.Exec(file, convertToExt);
                                readStream.CopyTo(zip);
                                if (!string.IsNullOrEmpty(convertToExt))
                                {
                                    filesMessageService.Send(file, headers, MessageAction.FileDownloadedAs, file.Title, convertToExt);
                                }
                                else
                                {
                                    filesMessageService.Send(file, headers, MessageAction.FileDownloaded, file.Title);
                                }
                            }
                            else
                            {
                                using var readStream = FileDao.GetFileStream(file);
                                readStream.CopyTo(zip);
                                filesMessageService.Send(file, headers, MessageAction.FileDownloaded, file.Title);
                            }
                        }
                        catch (Exception ex)
                        {
                            Error = ex.Message;
                            Logger.Error(Error, ex);
                        }
                    }
                    counter++;
                }

                ProgressStep();
            }
        }

        private void ReplaceLongPath(ItemNameValueCollection<T> entriesPathId)
        {
            foreach (var path in new List<string>(entriesPathId.AllKeys))
            {
                CancellationToken.ThrowIfCancellationRequested();

                if (200 >= path.Length || 0 >= path.IndexOf('/')) continue;

                var ids = entriesPathId[path];
                entriesPathId.Remove(path);

                var newtitle = "LONG_FOLDER_NAME" + path.Substring(path.LastIndexOf('/'));
                entriesPathId.Add(newtitle, ids);
            }
        }
    }

    internal class ItemNameValueCollection<T>
    {
        private readonly Dictionary<string, List<T>> dic = new Dictionary<string, List<T>>();


        public IEnumerable<string> AllKeys
        {
            get { return dic.Keys; }
        }

        public IEnumerable<T> this[string name]
        {
            get { return dic[name].ToArray(); }
        }

        public int Count
        {
            get { return dic.Count; }
        }

        public void Add(string name, T value)
        {
            if (!dic.ContainsKey(name))
            {
                dic.Add(name, new List<T>());
            }
            dic[name].Add(value);
        }

        public void Add(ItemNameValueCollection<T> collection)
        {
            foreach (var key in collection.AllKeys)
            {
                foreach (var value in collection[key])
                {
                    Add(key, value);
                }
            }
        }

        public void Add(string name, IEnumerable<T> values)
        {
            if (!dic.ContainsKey(name))
            {
                dic.Add(name, new List<T>());
            }
            dic[name].AddRange(values);
        }

        public void Remove(string name)
        {
            dic.Remove(name);
        }
    }
}