using System;
using Shouldly;
using Mapperion.Model;
using Xunit;

namespace Mapperion.Tests
{
    public sealed class TypeMapKeyTests
    {
        [Fact]
        public void Equal_type_pairs_are_equal()
        {
            var a = new TypeMapKey(typeof(string), typeof(int));
            var b = new TypeMapKey(typeof(string), typeof(int));

            a.ShouldBe(b);
            a.GetHashCode().ShouldBe(b.GetHashCode());
            (a == b).ShouldBeTrue();
        }

        [Fact]
        public void Reversed_type_pairs_are_not_equal()
        {
            var forward = new TypeMapKey(typeof(string), typeof(int));
            var reverse = new TypeMapKey(typeof(int), typeof(string));

            forward.ShouldNotBe(reverse);
            (forward != reverse).ShouldBeTrue();
        }

        [Fact]
        public void Null_types_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => new TypeMapKey(null!, typeof(int)));
            Should.Throw<ArgumentNullException>(() => new TypeMapKey(typeof(int), null!));
        }
    }
}
