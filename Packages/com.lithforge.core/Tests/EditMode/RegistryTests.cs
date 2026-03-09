using System;
using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Core.Registry;
using NUnit.Framework;

namespace Lithforge.Core.Tests
{
    [TestFixture]
    public sealed class RegistryTests
    {
        [Test]
        public void Register_ValidEntry_CanBuildAndRetrieve()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            ResourceId id = new ResourceId("lithforge", "stone");
            builder.Register(id, "stone_value");

            Registry<string> registry = builder.Build();

            Assert.AreEqual("stone_value", registry.Get(id));
        }

        [Test]
        public void Register_MultipleEntries_AllRetrievable()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            ResourceId id1 = new ResourceId("lithforge", "stone");
            ResourceId id2 = new ResourceId("lithforge", "dirt");
            builder.Register(id1, "stone_value");
            builder.Register(id2, "dirt_value");

            Registry<string> registry = builder.Build();

            Assert.AreEqual(2, registry.Count);
            Assert.AreEqual("stone_value", registry.Get(id1));
            Assert.AreEqual("dirt_value", registry.Get(id2));
        }

        [Test]
        public void Register_DuplicateId_ThrowsArgumentException()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            ResourceId id = new ResourceId("lithforge", "stone");
            builder.Register(id, "first");

            Assert.Throws<ArgumentException>(() => builder.Register(id, "second"));
        }

        [Test]
        public void Register_AfterBuild_ThrowsInvalidOperationException()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            builder.Build();

            ResourceId id = new ResourceId("lithforge", "stone");
            Assert.Throws<InvalidOperationException>(() => builder.Register(id, "value"));
        }

        [Test]
        public void Build_CalledTwice_ThrowsInvalidOperationException()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            builder.Build();

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Contains_RegisteredId_ReturnsTrue()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            ResourceId id = new ResourceId("lithforge", "stone");
            builder.Register(id, "value");

            Registry<string> registry = builder.Build();

            Assert.IsTrue(registry.Contains(id));
        }

        [Test]
        public void Contains_UnregisteredId_ReturnsFalse()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            Registry<string> registry = builder.Build();

            ResourceId id = new ResourceId("lithforge", "unknown");
            Assert.IsFalse(registry.Contains(id));
        }

        [Test]
        public void Get_UnregisteredId_ThrowsKeyNotFoundException()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            Registry<string> registry = builder.Build();

            ResourceId id = new ResourceId("lithforge", "unknown");
            Assert.Throws<KeyNotFoundException>(() => registry.Get(id));
        }

        [Test]
        public void GetAll_ReturnsAllEntries()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            ResourceId id1 = new ResourceId("lithforge", "stone");
            ResourceId id2 = new ResourceId("lithforge", "dirt");
            builder.Register(id1, "stone_value");
            builder.Register(id2, "dirt_value");

            Registry<string> registry = builder.Build();
            IReadOnlyDictionary<ResourceId, string> all = registry.GetAll();

            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void BuilderContains_BeforeBuild_Works()
        {
            RegistryBuilder<string> builder = new RegistryBuilder<string>();
            ResourceId id = new ResourceId("lithforge", "stone");
            builder.Register(id, "value");

            Assert.IsTrue(builder.Contains(id));
            Assert.AreEqual(1, builder.Count);
        }
    }
}
