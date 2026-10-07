using System;
using System.Collections.Generic;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Outcome of a registry operation (ADR 0013: environment problems are results).</summary>
    public enum RegistryStatus
    {
        /// <summary>The operation succeeded.</summary>
        Ok,
        /// <summary>The key or value does not exist.</summary>
        Missing,
        /// <summary>Access denied (permissions, policies, another account's key).</summary>
        AccessDenied,
        /// <summary>Any other error of the registry (key marked for deletion, hive not loaded, ...).</summary>
        IoError,
        /// <summary>A key or value name Windows does not accept (length, characters).</summary>
        InvalidName
    }

    /// <summary>Status of a registry operation that returns no value.</summary>
    public readonly struct RegistryResult
    {
        private RegistryResult(RegistryStatus status, string detail)
        {
            Status = status;
            Detail = detail;
        }

        public static RegistryResult Success
        {
            get { return new RegistryResult(RegistryStatus.Ok, null); }
        }

        public RegistryStatus Status { get; }

        /// <summary>The message of the underlying error, for the log; null on success.</summary>
        public string Detail { get; }

        public bool IsOk
        {
            get { return Status == RegistryStatus.Ok; }
        }

        public static RegistryResult Failure(RegistryStatus status, string detail)
        {
            if (status == RegistryStatus.Ok)
                throw new ArgumentException("A failure needs a status other than Ok.", nameof(status));
            return new RegistryResult(status, detail);
        }

        public override string ToString()
        {
            return Detail == null ? Status.ToString() : Status + " (" + Detail + ")";
        }
    }

    /// <summary>Status and value of a registry read.</summary>
    public readonly struct RegistryResult<T>
    {
        private RegistryResult(RegistryStatus status, T value, string detail)
        {
            Status = status;
            Value = value;
            Detail = detail;
        }

        public RegistryStatus Status { get; }

        /// <summary>The value; the default of <typeparamref name="T"/> unless <see cref="IsOk"/>.</summary>
        public T Value { get; }

        /// <summary>The message of the underlying error, for the log; null on success.</summary>
        public string Detail { get; }

        public bool IsOk
        {
            get { return Status == RegistryStatus.Ok; }
        }

        public static RegistryResult<T> Success(T value)
        {
            return new RegistryResult<T>(RegistryStatus.Ok, value, null);
        }

        public static RegistryResult<T> Failure(RegistryStatus status, string detail)
        {
            if (status == RegistryStatus.Ok)
                throw new ArgumentException("A failure needs a status other than Ok.", nameof(status));
            return new RegistryResult<T>(status, default, detail);
        }

        public override string ToString()
        {
            return Detail == null ? Status.ToString() : Status + " (" + Detail + ")";
        }
    }

    /// <summary>
    /// Access to the Windows registry with an explicit hive and view for every key (ADR 0006, contract 0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here throws for a missing key or value, denied access or an error of the registry: those are
    /// returned as <see cref="RegistryStatus"/> (ADR 0013). Null arguments and operations on a hive itself
    /// (create or delete) are programming errors and throw.
    /// </para>
    /// <para>
    /// Value names are compared ignoring case; the empty name is the default value of a key. Every change of the
    /// launcher has to pass the registry write policy (ADR 0007).
    /// </para>
    /// </remarks>
    public interface IRegistry
    {
        /// <summary><see cref="RegistryStatus.Ok"/> if the key exists, <see cref="RegistryStatus.Missing"/> if not.</summary>
        RegistryResult ProbeKey(RegistryLocation key);

        /// <summary>
        /// True if the key itself is a symbolic registry link (<c>REG_LINK</c>), looked at without following it
        /// (<see cref="RegistryStatus.Missing"/> if it does not exist). The other operations follow links, so code that walks a
        /// tree to back it up and delete it must not enter one (security review: a link can point at
        /// <c>Software\Sierra\CDKeys</c>).
        /// </summary>
        RegistryResult<bool> IsLink(RegistryLocation key);

        /// <summary>Reads a value (<see cref="RegistryStatus.Missing"/> if the key or the value does not exist).</summary>
        RegistryResult<RegistryValue> GetValue(RegistryLocation key, string valueName);

        /// <summary>The names of the values of a key, sorted ignoring case.</summary>
        RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key);

        /// <summary>The names of the subkeys of a key, sorted ignoring case.</summary>
        RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key);

        /// <summary>Creates a key and its missing parents; succeeds if it exists already.</summary>
        RegistryResult CreateSubKey(RegistryLocation key);

        /// <summary>
        /// Writes a value of an existing key (<see cref="RegistryStatus.Missing"/> if the key does not exist),
        /// replacing a value of the same name and any type.
        /// </summary>
        RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value);

        /// <summary>Deletes a value (<see cref="RegistryStatus.Missing"/> if the key or value does not exist).</summary>
        RegistryResult DeleteValue(RegistryLocation key, string valueName);

        /// <summary>Deletes a key with all its subkeys (<see cref="RegistryStatus.Missing"/> if it does not exist).</summary>
        RegistryResult DeleteSubKeyTree(RegistryLocation key);
    }
}
