using OrderHub.Core.Domain;

namespace OrderHub.Core.Ai;

public class OrderSearchQuery
{
    public OrderStatus? Status { get; set; }
    public CustomerTier? MemberTier { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public bool HasAnyFilter =>
        Status.HasValue || MemberTier.HasValue || DateFrom.HasValue || DateTo.HasValue;
}
