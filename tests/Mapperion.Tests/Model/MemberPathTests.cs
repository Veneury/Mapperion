using System;
using System.Collections.Generic;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Model
{
    public sealed class MemberPathTests
    {
        private static MemberPath CustomerAddressCity()
        {
            return MemberPath.Of(Members.Property<Order>(nameof(Order.Customer)))
                .Append(Members.Property<Customer>(nameof(Customer.Address)))
                .Append(Members.Property<Address>(nameof(Address.City)));
        }

        [Fact]
        public void Single_step_path_is_not_flattened()
        {
            MemberPath path = MemberPath.Of(Members.Property<Order>(nameof(Order.Id)));

            path.Length.ShouldBe(1);
            path.IsFlattened.ShouldBeFalse();
            path.MemberType.ShouldBe(typeof(int));
            path.ToString().ShouldBe("Id");
        }

        [Fact]
        public void Appending_builds_a_flattening_path()
        {
            MemberPath path = CustomerAddressCity();

            path.Length.ShouldBe(3);
            path.IsFlattened.ShouldBeTrue();
            path.Leaf.Name.ShouldBe("City");
            path.MemberType.ShouldBe(typeof(string));
            path.ToString().ShouldBe("Customer.Address.City");
        }

        [Fact]
        public void Appending_does_not_mutate_the_original()
        {
            MemberPath original = MemberPath.Of(Members.Property<Order>(nameof(Order.Customer)));

            original.Append(Members.Property<Customer>(nameof(Customer.Name)));

            original.Length.ShouldBe(1);
        }

        [Fact]
        public void Paths_over_the_same_members_are_equal()
        {
            CustomerAddressCity().ShouldBe(CustomerAddressCity());
            CustomerAddressCity().GetHashCode().ShouldBe(CustomerAddressCity().GetHashCode());
        }

        [Fact]
        public void Paths_over_different_members_are_not_equal()
        {
            MemberPath name = MemberPath.Of(Members.Property<Order>(nameof(Order.Customer)))
                .Append(Members.Property<Customer>(nameof(Customer.Name)));

            name.ShouldNotBe(CustomerAddressCity());
        }

        [Fact]
        public void Empty_path_is_rejected()
        {
            Should.Throw<ArgumentException>(() => new MemberPath(new List<MemberDescriptor>()));
        }

        [Fact]
        public void Null_steps_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => new MemberPath(null!));
            Should.Throw<ArgumentException>(() => new MemberPath(new MemberDescriptor[] { null! }));
        }
    }
}
