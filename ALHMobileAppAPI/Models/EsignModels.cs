using System;
using System.Collections.Generic;

namespace ALHMobileAppAPI.Esign.Models
{
    public enum DocumentStatus
    {
        Draft,
        Pending,
        PartiallySigned,
        Completed,
        Rejected,
        Cancelled
    }

    public enum RecipientRole
    {
        Sign,
        Approve,
        View,
        CC
    }

    public enum RecipientStatus
    {
        Pending,
        Sent,
        Viewed,
        Signed,
        Rejected,
        Declined
    }

    public enum FieldType
    {
        Signature,
        Stamp,
        Text,
        Date,
        Initial,
        Checkbox
    }

    public class EsignDocument
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string OriginalGcsPath { get; set; }
        public string WorkingGcsPath { get; set; }
        public string FinalGcsPath { get; set; }
        public DocumentStatus Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? SentOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public int? DaysToComplete { get; set; }
        public int? ReminderDays { get; set; }
        public string Note { get; set; }
        public bool IsOrdered { get; set; }
        public bool IsDeleted { get; set; }

        public List<EsignRecipient> Recipients { get; set; } = new List<EsignRecipient>();
        public List<EsignField> Fields { get; set; } = new List<EsignField>();
    }

    public class EsignRecipient
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public RecipientRole Role { get; set; }
        public int? SigningOrder { get; set; }
        public RecipientStatus Status { get; set; }
        public string DeliveryMethod { get; set; }
        public Guid AccessToken { get; set; }
        public DateTime? SentOn { get; set; }
        public DateTime? ViewedOn { get; set; }
        public DateTime? SignedOn { get; set; }
        public string RejectReason { get; set; }
    }

    public class EsignField
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int RecipientId { get; set; }
        public FieldType FieldType { get; set; }
        public int PageNumber { get; set; }
        public decimal XPct { get; set; }
        public decimal YPct { get; set; }
        public decimal WidthPct { get; set; }
        public decimal HeightPct { get; set; }
        public string Value { get; set; }
        public bool IsRequired { get; set; }
        public DateTime? FilledOn { get; set; }
    }

    public class EsignAuditLog
    {
        public long Id { get; set; }
        public int DocumentId { get; set; }
        public int? RecipientId { get; set; }
        public string Action { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Details { get; set; }
    }
}
