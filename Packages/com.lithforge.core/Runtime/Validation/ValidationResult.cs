using System.Collections.Generic;

namespace Lithforge.Core.Validation
{
    /// <summary>
    /// Result of validating a content definition.
    /// Contains lists of errors (fatal) and warnings (non-fatal).
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>Accumulated fatal validation errors.</summary>
        private readonly List<string> _errors = new();

        /// <summary>Accumulated non-fatal validation warnings.</summary>
        private readonly List<string> _warnings = new();

        /// <summary>Read-only view of all fatal errors collected during validation.</summary>
        public IReadOnlyList<string> Errors
        {
            get { return _errors; }
        }

        /// <summary>Read-only view of all non-fatal warnings collected during validation.</summary>
        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>True when no fatal errors were recorded.</summary>
        public bool IsValid
        {
            get { return _errors.Count == 0; }
        }

        /// <summary>Records a fatal validation error.</summary>
        public void AddError(string message)
        {
            _errors.Add(message);
        }

        /// <summary>Records a non-fatal validation warning.</summary>
        public void AddWarning(string message)
        {
            _warnings.Add(message);
        }

        /// <summary>Copies all errors and warnings from another result into this one.</summary>
        public void Merge(ValidationResult other)
        {
            _errors.AddRange(other._errors);
            _warnings.AddRange(other._warnings);
        }
    }
}
