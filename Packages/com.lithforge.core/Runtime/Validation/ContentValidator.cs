using System;

using Lithforge.Core.Data;

namespace Lithforge.Core.Validation
{
    /// <summary>
    ///     Validates content definitions against expected rules.
    ///     Collects all errors and warnings into a ValidationResult.
    /// </summary>
    public sealed class ContentValidator
    {
        /// <summary>
        ///     Validates a ResourceId string format.
        /// </summary>
        public ValidationResult ValidateResourceId(string raw)
        {
            ValidationResult result = new();

            if (string.IsNullOrEmpty(raw))
            {
                result.AddError("ResourceId is null or empty.");

                return result;
            }

            if (!ResourceId.TryParse(raw, out ResourceId _))
            {
                result.AddError(
                    $"ResourceId '{raw}' does not match required format '^[a-z0-9_]+:[a-z0-9_/]+$'.");
            }

            return result;
        }

        /// <summary>
        ///     Validates that a required field is present and non-null.
        /// </summary>
        public void ValidateRequiredField(ValidationResult result, string fieldName, object value, string context)
        {
            if (value == null)
            {
                result.AddError($"[{context}] Required field '{fieldName}' is missing.");
            }
        }

        /// <summary>
        ///     Validates that a numeric value is within a given range.
        /// </summary>
        public void ValidateRange(
            ValidationResult result,
            string fieldName,
            double value,
            double min,
            double max,
            string context)
        {
            if (value < min || value > max)
            {
                result.AddError(
                    $"[{context}] Field '{fieldName}' value {value} is out of range [{min}, {max}].");
            }
        }

        /// <summary>
        ///     Validates that an enum-like string field has an allowed value.
        /// </summary>
        public void ValidateEnumField(
            ValidationResult result,
            string fieldName,
            string value,
            string[] allowedValues,
            string context)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            bool found = false;

            for (int i = 0; i < allowedValues.Length; i++)
            {
                if (string.Equals(value, allowedValues[i], StringComparison.Ordinal))
                {
                    found = true;

                    break;
                }
            }

            if (!found)
            {
                result.AddError(
                    $"[{context}] Field '{fieldName}' has invalid value '{value}'. " +
                    $"Allowed: {string.Join(", ", allowedValues)}.");
            }
        }
    }
}
