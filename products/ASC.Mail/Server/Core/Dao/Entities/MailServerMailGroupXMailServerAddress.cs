﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Mail.Core.Dao.Entities
{
    [Table("mail_server_mail_group_x_mail_server_address")]
    public partial class MailServerMailGroupXMailServerAddress
    {
        [Key]
        [Column("id_address", TypeName = "int(11)")]
        public int IdAddress { get; set; }
        [Key]
        [Column("id_mail_group", TypeName = "int(11)")]
        public int IdMailGroup { get; set; }
    }
}