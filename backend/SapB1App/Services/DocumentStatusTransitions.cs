using SapB1App.Models;

namespace SapB1App.Services;

public static class DocumentStatusTransitions
{
    public static bool CanTransition(QuoteStatus from, QuoteStatus to) => from switch
    {
        QuoteStatus.Pending => to is QuoteStatus.Accepted or QuoteStatus.Rejected,
        QuoteStatus.Accepted => false,
        QuoteStatus.Rejected => false,
        _ => false
    };

    public static bool CanTransition(DeliveryNoteStatus from, DeliveryNoteStatus to) => from switch
    {
        DeliveryNoteStatus.InProgress => to == DeliveryNoteStatus.Delivered,
        DeliveryNoteStatus.Delivered => false,
        _ => false
    };

    public static bool CanTransition(InvoiceStatus from, InvoiceStatus to) => from switch
    {
        InvoiceStatus.Unpaid => to == InvoiceStatus.Paid,
        InvoiceStatus.Paid => false,
        _ => false
    };

    public static bool CanTransition(ReturnStatus from, ReturnStatus to) => from switch
    {
        ReturnStatus.Pending => to == ReturnStatus.Validated,
        ReturnStatus.Validated => false,
        _ => false
    };
}
