﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Calendar.Core.Dao.Models
{
    [Table("calendar_event_user")]
    public partial class CalendarEventUser
    {
        [Key]
        [Column("event_id", TypeName = "int(10)")]
        public int EventId { get; set; }
        [Key]
        [Column("user_id", TypeName = "char(38)")]
        public string UserId { get; set; }
        [Column("alert_type", TypeName = "smallint(6)")]
        public short AlertType { get; set; }
        [Column("is_unsubscribe", TypeName = "smallint(2)")]
        public short IsUnsubscribe { get; set; }
    }
}