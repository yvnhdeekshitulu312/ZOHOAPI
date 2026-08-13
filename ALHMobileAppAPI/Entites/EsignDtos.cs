using System;
using System.Collections.Generic;

namespace ALHMobileAppAPI.Esign.DTOs
{
    // ---------- Upload ----------
    public class UploadDocumentResponse
    {
        public int DocumentId { get; set; }
        public string Name { get; set; }
        public string OriginalGcsPath { get; set; }
    }

    // ---------- Send for signature ----------
    public class SendDocumentRequest
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; }
        public bool IsOrdered { get; set; }
        public int? DaysToComplete { get; set; }
        public int? ReminderDays { get; set; }
        public string Note { get; set; }
        public List<RecipientDto> Recipients { get; set; }
        public List<FieldDto> Fields { get; set; }
    }

    public class RecipientDto
    {
        // Client-side temp id (e.g. "r1") used only to link Fields -> Recipients
        // in the same payload before the server assigns real DB ids.
        public string ClientId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Role { get; set; } // Sign / Approve / View / CC
        public int? SigningOrder { get; set; }
        public string DeliveryMethod { get; set; } // Email / Teams / Slack
    }

    public class FieldDto
    {
        public string RecipientClientId { get; set; }
        public string FieldType { get; set; } // Signature / Stamp / Text / Date / Initial / Checkbox
        public int PageNumber { get; set; }
        public decimal XPct { get; set; }
        public decimal YPct { get; set; }
        public decimal WidthPct { get; set; }
        public decimal HeightPct { get; set; }
        public bool IsRequired { get; set; }
    }

    // ---------- Fetch for viewer ----------
    public class DocumentDetailResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string ViewerGcsUrl { get; set; } // blob URL to render, same pattern as your scanned-docs viewer
        public bool IsOrdered { get; set; }
        public List<RecipientSummaryDto> Recipients { get; set; }
        public List<FieldSummaryDto> Fields { get; set; }
        public List<string> PageImages { get; set; } = new List<string>();
    }

    public class RecipientSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public int? SigningOrder { get; set; }
    }

    public class FieldSummaryDto
    {
        public int Id { get; set; }
        public int RecipientId { get; set; }
        public string FieldType { get; set; }
        public int PageNumber { get; set; }
        public decimal XPct { get; set; }
        public decimal YPct { get; set; }
        public decimal WidthPct { get; set; }
        public decimal HeightPct { get; set; }
        public string Value { get; set; }
        public bool IsRequired { get; set; }
    }

    // ---------- Sign ----------
    public class SignDocumentRequest
    {
        public Guid AccessToken { get; set; }
        public List<FieldValueDto> FieldValues { get; set; }
    }

    public class FieldValueDto
    {
        public int FieldId { get; set; }
        // For Signature/Stamp: base64 PNG (transparent bg) drawn on signature_pad.
        // For Text/Date/Checkbox: plain string value.
        public string Value { get; set; }
    }

    public class RejectDocumentRequest
    {
        public Guid AccessToken { get; set; }
        public string Reason { get; set; }
    }

    public class SignAsUserRequest
    {
        public int DocumentId { get; set; }
        public List<FieldValueDto> FieldValues { get; set; }
    }
}
