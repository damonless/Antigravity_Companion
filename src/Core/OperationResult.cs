using System;
using System.Collections.Generic;

namespace AntigravityCompanion
{
    /// <summary>
    /// Immutable operation outcome wrapper carrying typed payload, error detail, and non-fatal warnings.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public sealed class OperationResult<T>
    {
        private readonly bool _ok;
        private readonly T _data;
        private readonly string _errorMessage;
        private readonly List<string> _warnings;

        private OperationResult(bool ok, T data, string errorMessage, List<string> warnings)
        {
            _ok = ok;
            _data = data;
            _errorMessage = errorMessage ?? string.Empty;
            _warnings = warnings != null ? new List<string>(warnings) : new List<string>();
        }

        public bool Ok
        {
            get { return _ok; }
        }

        public T Data
        {
            get { return _data; }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
        }

        public List<string> Warnings
        {
            get { return new List<string>(_warnings); }
        }

        public static OperationResult<T> Success(T data, List<string> warnings = null)
        {
            return new OperationResult<T>(true, data, string.Empty, warnings);
        }

        public static OperationResult<T> Fail(string error, List<string> warnings = null)
        {
            string msg = string.IsNullOrEmpty(error) ? "Operation failed." : error;
            return new OperationResult<T>(false, default(T), msg, warnings);
        }
    }

    /// <summary>
    /// Non-generic helper factories for OperationResult&lt;T&gt;.
    /// </summary>
    public static class OperationResult
    {
        public static OperationResult<T> Success<T>(T data, List<string> warnings = null)
        {
            return OperationResult<T>.Success(data, warnings);
        }

        public static OperationResult<object> Success(List<string> warnings = null)
        {
            return OperationResult<object>.Success(null, warnings);
        }

        public static OperationResult<T> Fail<T>(string error, List<string> warnings = null)
        {
            return OperationResult<T>.Fail(error, warnings);
        }

        public static OperationResult<object> Fail(string error, List<string> warnings = null)
        {
            return OperationResult<object>.Fail(error, warnings);
        }
    }
}
