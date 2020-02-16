﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Mail.Core.Dao.Entities
{
    [Table("mail_folder_counters")]
    public partial class MailFolderCounters
    {
        [Key]
        [Column("tenant", TypeName = "int(11)")]
        public int Tenant { get; set; }
        [Key]
        [Column("id_user", TypeName = "varchar(255)")]
        public string IdUser { get; set; }
        [Key]
        [Column("folder", TypeName = "smallint(5) unsigned")]
        public ushort Folder { get; set; }
        [Column("unread_messages_count", TypeName = "int(10) unsigned")]
        public uint UnreadMessagesCount { get; set; }
        [Column("total_messages_count", TypeName = "int(10) unsigned")]
        public uint TotalMessagesCount { get; set; }
        [Column("unread_conversations_count", TypeName = "int(10) unsigned")]
        public uint UnreadConversationsCount { get; set; }
        [Column("total_conversations_count", TypeName = "int(10) unsigned")]
        public uint TotalConversationsCount { get; set; }
        [Column("time_modified", TypeName = "timestamp")]
        public DateTime TimeModified { get; set; }
    }
}