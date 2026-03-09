using Lithforge.Core.Validation;
using NUnit.Framework;

namespace Lithforge.Core.Tests
{
    [TestFixture]
    public sealed class ContentValidatorTests
    {
        private ContentValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _validator = new ContentValidator();
        }

        [Test]
        public void ValidateResourceId_ValidFormat_ReturnsValid()
        {
            ValidationResult result = _validator.ValidateResourceId("lithforge:stone");

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [Test]
        public void ValidateResourceId_WithSlashes_ReturnsValid()
        {
            ValidationResult result = _validator.ValidateResourceId("lithforge:blocks/stone");

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateResourceId_Null_ReturnsError()
        {
            ValidationResult result = _validator.ValidateResourceId(null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
        }

        [Test]
        public void ValidateResourceId_UpperCase_ReturnsError()
        {
            ValidationResult result = _validator.ValidateResourceId("Lithforge:Stone");

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void ValidateRequiredField_NullValue_AddsError()
        {
            ValidationResult result = new ValidationResult();
            _validator.ValidateRequiredField(result, "name", null, "test");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
        }

        [Test]
        public void ValidateRequiredField_NonNullValue_NoError()
        {
            ValidationResult result = new ValidationResult();
            _validator.ValidateRequiredField(result, "name", "value", "test");

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateRange_InRange_NoError()
        {
            ValidationResult result = new ValidationResult();
            _validator.ValidateRange(result, "emission", 10, 0, 15, "test");

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateRange_OutOfRange_AddsError()
        {
            ValidationResult result = new ValidationResult();
            _validator.ValidateRange(result, "emission", 20, 0, 15, "test");

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void ValidateEnumField_ValidValue_NoError()
        {
            ValidationResult result = new ValidationResult();
            _validator.ValidateEnumField(result, "render_layer", "opaque",
                new string[] { "opaque", "cutout", "translucent" }, "test");

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateEnumField_InvalidValue_AddsError()
        {
            ValidationResult result = new ValidationResult();
            _validator.ValidateEnumField(result, "render_layer", "invalid",
                new string[] { "opaque", "cutout", "translucent" }, "test");

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void ValidationResult_Merge_CombinesErrorsAndWarnings()
        {
            ValidationResult a = new ValidationResult();
            a.AddError("error1");
            a.AddWarning("warn1");

            ValidationResult b = new ValidationResult();
            b.AddError("error2");

            a.Merge(b);

            Assert.AreEqual(2, a.Errors.Count);
            Assert.AreEqual(1, a.Warnings.Count);
        }
    }
}
