using System.Collections.Generic;

namespace Lithforge.Core.Validation
{
    /// <summary>
    /// Result of validating a content definition.
    /// Contains lists of errors (fatal) and warnings (non-fatal).
    /// </summary>
    public sealed class ValidationResult
    {
        private readonly List<string> _errors;
        private readonly List<string> _warnings;

        public IReadOnlyList<string> Errors
        {
            get { return _errors; }
        }

        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        public bool IsValid
        {
            get { return _errors.Count == 0; }
        }

        public ValidationResult()
        {
            _errors = new List<string>();
            _warnings = new List<string>();
        }

        public void AddError(string message)
        {
            _errors.Add(message);
        }

        public void AddWarning(string message)
        {
            _warnings.Add(message);
        }

        public void Merge(ValidationResult other)
        {
            _errors.AddRange(other._errors);
            _warnings.AddRange(other._warnings);
        }
    }
}
