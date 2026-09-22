using System.Reflection;
using Mapperion.Model;

namespace Mapperion.Tests.Model
{
    public sealed class Address
    {
        public string City { get; set; } = string.Empty;
    }

    public sealed class Customer
    {
        public string Name { get; set; } = string.Empty;

        public Address Address { get; set; } = new Address();
    }

    public sealed class Order
    {
        public int Id { get; set; }

        public Customer Customer { get; set; } = new Customer();

        public string ReadOnlyCode { get; } = string.Empty;

        public string GetDisplayName() => "order";

        public void DoNothing()
        {
        }

        public int this[int index] => index;
    }

    public sealed class OrderDto
    {
        public int Id { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerAddressCity { get; set; } = string.Empty;
    }

    internal static class Members
    {
        internal static MemberDescriptor Property<T>(string name)
        {
            PropertyInfo property = typeof(T).GetProperty(name)!;
            return MemberDescriptor.ForProperty(property);
        }

        internal static MemberDescriptor Method<T>(string name)
        {
            MethodInfo method = typeof(T).GetMethod(name)!;
            return MemberDescriptor.ForMethod(method);
        }
    }
}
