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


using System.Threading.Tasks;
using System.Web;

using ASC.Common;
using ASC.Common.Utils;
using ASC.Core.Common;
using ASC.Files.Core;
using ASC.Files.Core.Security;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;

using FileShare = ASC.Files.Core.Security.FileShare;

namespace ASC.Web.Files.Utils
{
    public class FileShareLink
    {
        public FileUtility FileUtility { get; }
        public FilesLinkUtility FilesLinkUtility { get; }
        public BaseCommonLinkUtility BaseCommonLinkUtility { get; }
        public Global Global { get; }
        public FileSecurity FileSecurity { get; }

        public FileShareLink(
            FileUtility fileUtility,
            FilesLinkUtility filesLinkUtility,
            BaseCommonLinkUtility baseCommonLinkUtility,
            Global global,
            FileSecurity fileSecurity)
        {
            FileUtility = fileUtility;
            FilesLinkUtility = filesLinkUtility;
            BaseCommonLinkUtility = baseCommonLinkUtility;
            Global = global;
            FileSecurity = fileSecurity;
        }

        public string GetLink<T>(File<T> file, bool withHash = true)
        {
            var url = file.DownloadUrl;

            if (FileUtility.CanWebView(file.Title))
                url = FilesLinkUtility.GetFileWebPreviewUrl(FileUtility, file.Title, file.ID);

            if (withHash)
            {
                var linkParams = CreateKey(file.ID);
                url += "&" + FilesLinkUtility.DocShareKey + "=" + HttpUtility.UrlEncode(linkParams);
            }

            return BaseCommonLinkUtility.GetFullAbsolutePath(url);
        }

        public string CreateKey<T>(T fileId)
        {
            return Signature.Create(fileId, Global.GetDocDbKey());
        }

        public string Parse(string doc)
        {
            return Signature.Read<string>(doc ?? string.Empty, Global.GetDocDbKey());
        }
        public T Parse<T>(string doc)
        {
            return Signature.Read<T>(doc ?? string.Empty, Global.GetDocDbKey());
        }

        public async Task<(bool, File<T>)> Check<T>(string doc, bool checkRead, IFileDao<T> fileDao)
        {
            var (fileShare, file) = await Check(doc, fileDao);
            return ((!checkRead && (fileShare == FileShare.ReadWrite || fileShare == FileShare.Review || fileShare == FileShare.FillForms || fileShare == FileShare.Comment))
                || (checkRead && fileShare != FileShare.Restrict), file);
        }

        public async Task<(FileShare, File<T>)> Check<T>(string doc, IFileDao<T> fileDao)
        {
            if (string.IsNullOrEmpty(doc)) return (FileShare.Restrict, null);
            var fileId = Parse<T>(doc);
            var file = await fileDao.GetFile(fileId);
            if (file == null) return (FileShare.Restrict, file);

            var filesSecurity = FileSecurity;
            if (await filesSecurity.CanEdit(file, FileConstant.ShareLinkId)) return (FileShare.ReadWrite, file);
            if (await filesSecurity.CanReview(file, FileConstant.ShareLinkId)) return (FileShare.Review, file);
            if (await filesSecurity.CanFillForms(file, FileConstant.ShareLinkId)) return (FileShare.FillForms, file);
            if (await filesSecurity.CanComment(file, FileConstant.ShareLinkId)) return (FileShare.Comment, file);
            if (await filesSecurity.CanRead(file, FileConstant.ShareLinkId)) return (FileShare.Read, file);
            return (FileShare.Restrict, file);
        }
    }
    public static class FileShareLinkExtension
    {
        public static DIHelper AddFileShareLinkService(this DIHelper services)
        {
            services.TryAddScoped<FileShareLink>();
            return services
                .AddFilesLinkUtilityService()
                .AddFileUtilityService()
                .AddBaseCommonLinkUtilityService()
                .AddGlobalService()
                .AddFileSecurityService();
        }
    }
}