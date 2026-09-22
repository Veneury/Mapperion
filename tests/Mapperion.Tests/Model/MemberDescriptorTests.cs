using System;
using System.Reflection;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Model
{
    public sealed class MemberDescriptorTests
    {
        [Fact]
        public void Describes_a_readable_writable_property()
        {
            MemberDescriptor descriptor = Members.Property<Order>(nameof(Order.Id));

            descriptor.Name.ShouldBe("Id");
            descriptor.MemberType.ShouldBe(typeof(int));
            descriptor.Kind.ShouldBe(MemberKind.Property);
            descriptor.DeclaringType.ShouldBe(typeof(Order));
            descriptor.CanRead.ShouldBeTrue();
            descriptor.CanWrite.ShouldBeTrue();
        }

        [Fact]
        public void Get_only_property_is_not_writable()
        {
            MemberDescriptor descriptor = Members.Property<Order>(nameof(Order.ReadOnlyCode));

            descriptor.CanRead.ShouldBeTrue();
            descriptor.CanWrite.ShouldBeFalse();
        }

        [Fact]
        public void Parameterless_method_is_a_read_only_member()
        {
            MemberDescriptor descriptor = Members.Method<Order>(nameof(Order.GetDisplayName));

            descriptor.Kind.ShouldBe(MemberKind.Method);
            descriptor.MemberType.ShouldBe(typeof(string));
            descriptor.CanRead.ShouldBeTrue();
            descriptor.CanWrite.ShouldBeFalse();
        }

        [Fact]
        public void Void_method_is_rejected()
        {
            MethodInfo method = typeof(Order).GetMethod(nameof(Order.DoNothing))!;

            Should.Throw<ArgumentException>(() => MemberDescriptor.ForMethod(method));
        }

        [Fact]
        public void Indexer_is_rejected()
        {
            PropertyInfo indexer = typeof(Order).GetProperty("Item")!;

            Should.Throw<ArgumentException>(() => MemberDescriptor.ForProperty(indexer));
        }

        [Fact]
        public void Null_arguments_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => MemberDescriptor.ForProperty(null!));
            Should.Throw<ArgumentNullException>(() => MemberDescriptor.ForField(null!));
            Should.Throw<ArgumentNullException>(() => MemberDescriptor.ForMethod(null!));
        }

        [Fact]
        public void Descriptors_for_the_same_member_are_equal()
        {
            MemberDescriptor first = Members.Property<Order>(nameof(Order.Id));
            MemberDescriptor second = Members.Property<Order>(nameof(Order.Id));

            first.ShouldBe(second);
            first.GetHashCode().ShouldBe(second.GetHashCode());
        }

        [Fact]
        public void ToString_shows_declaring_type_and_name()
        {
            Members.Property<Order>(nameof(Order.Id)).ToString().ShouldBe("Order.Id");
        }
    }
}
