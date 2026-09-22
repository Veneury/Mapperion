using Mapperion.Tests.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class MemberExpressionParserTests
    {
        [Fact]
        public void A_destination_selector_that_is_not_a_member_access_is_rejected()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(() =>
                new MapperConfiguration(cfg => cfg
                    .CreateMap<Order, OrderDto>()
                    .ForMember(d => d.Id + 1, o => o.Ignore())));

            error.Message.ShouldContain("direct member access");
        }

        [Fact]
        public void A_nested_destination_selector_is_rejected()
        {
            Should.Throw<MapperConfigurationException>(() =>
                new MapperConfiguration(cfg => cfg
                    .CreateMap<OrderDto, Order>()
                    .ForMember(d => d.Customer.Name, o => o.Ignore())));
        }

        [Fact]
        public void A_boxing_conversion_around_the_destination_member_is_unwrapped()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember<object>(d => d.Id, o => o.Ignore()));

            config.Model.TypeMaps[0].FindMember(nameof(OrderDto.Id)).ShouldNotBeNull();
        }
    }
}
