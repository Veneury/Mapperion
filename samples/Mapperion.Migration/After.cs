using System;
using System.Globalization;
using System.Linq;
using Mapperion;

namespace Invoicing.After
{
    /// <summary>
    /// The configuration as it was, on AutoMapper 14. Nothing here is written to be easy to
    /// migrate: it reaches for the things an invoicing layer reaches for.
    /// </summary>
    public sealed class InvoiceProfile : Profile
    {
        public InvoiceProfile()
        {
            CreateMap<Invoice, InvoiceDto>()
                .ForMember(d => d.IssuedOn, o => o.MapFrom<IssuedOnResolver>())
                .ForMember(d => d.Net, o => o.MapFrom(s => s.Lines.Sum(l => l.Quantity * l.UnitPrice)))
                .ForMember(d => d.Tax, o => o.MapFrom(s => s.Lines.Sum(l => l.Quantity * l.UnitPrice * l.TaxRate)))
                .ForMember(d => d.Discount, o => o.NullSubstitute(0m))
                .ForMember(d => d.Notes, o => o.Condition(s => !s.Cancelled))
                .ForMember(d => d.PreparedBy, o => o.MapFrom<PreparedByResolver>())
                .ForMember(d => d.Internal, o => o.Ignore())
                .AfterMap((source, destination) =>
                {
                    if (source.Cancelled)
                    {
                        destination.Number = destination.Number + " (cancelled)";
                    }
                });

            CreateMap<InvoiceLine, LineDto>()
                .ForCtorParam("Amount", o => o.MapFrom(s => s.Quantity * s.UnitPrice));

            CreateMap<Currency, CurrencyCode>().ConvertUsing<CurrencyConverter>();
        }
    }

    /// <summary>The date as the wire wants it, which is not what ToString would give.</summary>
    public sealed class IssuedOnResolver : IValueResolver<Invoice, InvoiceDto, string>
    {
        public string Resolve(Invoice source, InvoiceDto destination, string member, ResolutionContext context)
        {
            return source.IssuedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Who ran this, handed in per operation rather than configured.</summary>
    /// <remarks>
    /// The one line the migration touched. Mapperion types the bag as
    /// <c>IDictionary&lt;string, object?&gt;</c> where AutoMapper types it as
    /// <c>IDictionary&lt;string, object&gt;</c>, which is the more truthful of the two — nothing
    /// stops a caller putting a null in it — and which is why the cast needs a `!` here that it
    /// did not need there.
    /// </remarks>
    public sealed class PreparedByResolver : IValueResolver<Invoice, InvoiceDto, string>
    {
        public string Resolve(Invoice source, InvoiceDto destination, string member, ResolutionContext context)
        {
            return context.Items.TryGetValue("user", out object? user) ? (string)user! : "unknown";
        }
    }

    public sealed class CurrencyConverter : ITypeConverter<Currency, CurrencyCode>
    {
        public CurrencyCode Convert(Currency source, CurrencyCode destination, ResolutionContext context)
        {
            return Enum.Parse<CurrencyCode>(source.ToString());
        }
    }

    public static class Setup
    {
        public static IMapper Build()
        {
            var configuration = new MapperConfiguration(cfg => cfg.AddProfile<InvoiceProfile>());

            configuration.AssertConfigurationIsValid();

            return configuration.CreateMapper();
        }

        public static InvoiceDto Map(IMapper mapper, Invoice invoice, string user) =>
            mapper.Map<Invoice, InvoiceDto>(invoice, options => options.Items["user"] = user);
    }
}
