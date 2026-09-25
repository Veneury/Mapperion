using System.Linq;

namespace Mapperion.Bookshop
{
    /// <summary>
    /// The list view, which never leaves the database as an entity.
    /// </summary>
    /// <remarks>
    /// Nothing here is configured except the one member the conventions cannot reach.
    /// <c>CustomerFullName</c> and <c>CustomerAddressCity</c> are found by flattening, two and
    /// three hops in, and <c>Status</c> crosses between two different enums by name.
    /// </remarks>
    public sealed class OrderListProfile : Profile
    {
        public OrderListProfile()
        {
            CreateMap<Order, OrderSummaryDto>()
                .ForMember(d => d.LineCount, o => o.MapFrom(s => s.Lines.Count));
        }
    }

    /// <summary>
    /// The detail view, mapped from an aggregate already loaded into memory.
    /// </summary>
    /// <remarks>
    /// The payment is the interesting part. <see cref="PaymentDto"/> is abstract because nothing
    /// is ever just a payment, and the map for the base declares which concrete pairs it hands
    /// over to.
    /// </remarks>
    public sealed class OrderDetailProfile : Profile
    {
        public OrderDetailProfile()
        {
            CreateMap<Order, OrderDetailDto>()
                .ForMember(d => d.Total, o => o.MapFrom(s => s.Lines.Sum(l => l.Quantity * l.UnitPrice)));

            // Flattening wants the prefix: a destination AddressCity would have found this on its
            // own. The contract says City, so the map says where City comes from.
            CreateMap<Customer, CustomerDto>()
                .ForMember(d => d.City, o => o.MapFrom(s => s.Address.City));

            CreateMap<OrderLine, OrderLineDto>();

            CreateMap<Payment, PaymentDto>()
                .Include<CardPayment, CardPaymentDto>()
                .Include<TransferPayment, TransferPaymentDto>();

            CreateMap<CardPayment, CardPaymentDto>().IncludeBase<Payment, PaymentDto>();

            CreateMap<TransferPayment, TransferPaymentDto>().IncludeBase<Payment, PaymentDto>();
        }
    }
}
