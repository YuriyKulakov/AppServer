﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using ASC.Web.Core.Calendars;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Calendar.Core.Dao.Models
{
    [Table("calendar_events")]
    public partial class CalendarEvents
    {
        [Key]
        [Column("id", TypeName = "int(10)")]
        public int Id { get; set; }
        [Column("tenant", TypeName = "int(11)")]
        public int Tenant { get; set; }
        [Required]
        [Column("name", TypeName = "varchar(255)")]
        public string Name { get; set; }
        [Required]
        [Column("description", TypeName = "text")]
        public string Description { get; set; }
        [Column("calendar_id", TypeName = "int(11)")]
        public int CalendarId { get; set; }
        [Column("start_date", TypeName = "datetime")]
        public DateTime StartDate { get; set; }
        [Column("end_date", TypeName = "datetime")]
        public DateTime EndDate { get; set; }
        [Column("update_date", TypeName = "datetime")]
        public DateTime? UpdateDate { get; set; }
        [Column("all_day_long", TypeName = "smallint(6)")]
        public int AllDayLong { get; set; }
        [Column("repeat_type", TypeName = "smallint(6)")]
        public short RepeatType { get; set; }
        [Required]
        [Column("owner_id", TypeName = "char(38)")]
        public Guid OwnerId { get; set; }
        [Column("alert_type", TypeName = "smallint(6)")]
        public int AlertType { get; set; }
        [Column("rrule", TypeName = "varchar(255)")]
        public string Rrule { get; set; }
        [Column("uid", TypeName = "varchar(255)")]
        public string Uid { get; set; }
        [Column("status", TypeName = "smallint(6)")]
        public int Status { get; set; }
    }
}