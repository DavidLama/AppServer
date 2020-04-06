﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Mail.Core.Dao.Entities
{
    [Table("mail_server_domain")]
    public partial class MailServerDomain
    {
        [Key]
        [Column("id", TypeName = "int(11)")]
        public int Id { get; set; }
        [Column("tenant", TypeName = "int(11)")]
        public int Tenant { get; set; }
        [Required]
        [Column("name", TypeName = "varchar(255)")]
        public string Name { get; set; }
        [Column("is_verified", TypeName = "int(10)")]
        public bool IsVerified { get; set; }
        [Column("date_added", TypeName = "datetime")]
        public DateTime DateAdded { get; set; }
        [Column("date_checked", TypeName = "datetime")]
        public DateTime DateChecked { get; set; }
    }
}