using System;
using System.Collections.Generic;

namespace AntigravityCompanion
{
    /// <summary>
    /// UI-agnostic command dispatcher routing named commands to registered handlers
    /// and returning immutable OperationResult&lt;Dictionary&lt;string, object&gt;&gt; payloads.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public class CompanionCommandDispatcher
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, Func<Dictionary<string, object>, OperationResult<Dictionary<string, object>>>> _handlers =
            new Dictionary<string, Func<Dictionary<string, object>, OperationResult<Dictionary<string, object>>>>(StringComparer.OrdinalIgnoreCase);

        public void Register(string command, Func<Dictionary<string, object>, OperationResult<Dictionary<string, object>>> handler)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command name cannot be null or whitespace.", "command");
            if (handler == null)
                throw new ArgumentNullException("handler");

            string normalized = command.Trim();
            lock (_sync)
            {
                _handlers[normalized] = handler;
            }
        }

        public bool Unregister(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            lock (_sync)
            {
                return _handlers.Remove(command.Trim());
            }
        }

        public bool HasCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            lock (_sync)
            {
                return _handlers.ContainsKey(command.Trim());
            }
        }

        public List<string> GetRegisteredCommands()
        {
            lock (_sync)
            {
                List<string> cmds = new List<string>(_handlers.Keys);
                cmds.Sort(StringComparer.OrdinalIgnoreCase);
                return cmds;
            }
        }

        public OperationResult<Dictionary<string, object>> Execute(string command, Dictionary<string, object> args)
        {
            if (string.IsNullOrWhiteSpace(command))
                return OperationResult<Dictionary<string, object>>.Fail("Command name cannot be empty.");

            string normalized = command.Trim();
            Func<Dictionary<string, object>, OperationResult<Dictionary<string, object>>> handler;
            lock (_sync)
            {
                if (!_handlers.TryGetValue(normalized, out handler) || handler == null)
                {
                    return OperationResult<Dictionary<string, object>>.Fail("Unknown command: " + normalized);
                }
            }

            Dictionary<string, object> safeArgs = args ?? new Dictionary<string, object>(StringComparer.Ordinal);
            try
            {
                OperationResult<Dictionary<string, object>> result = handler(safeArgs);
                if (result == null)
                    return OperationResult<Dictionary<string, object>>.Fail("Command handler returned null result: " + normalized);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult<Dictionary<string, object>>.Fail(ex.Message);
            }
        }

        public OperationResult<Dictionary<string, object>> ExecuteJson(string command, string argsJson)
        {
            Dictionary<string, object> args = null;
            if (!string.IsNullOrWhiteSpace(argsJson))
            {
                object parsed;
                try
                {
                    parsed = UnifiedJson.Parse(argsJson);
                }
                catch (Exception ex)
                {
                    return OperationResult<Dictionary<string, object>>.Fail("Invalid JSON arguments: " + ex.Message);
                }

                args = UnifiedJson.AsDict(parsed);
                if (args == null)
                    return OperationResult<Dictionary<string, object>>.Fail("JSON arguments root must be an object.");
            }

            return Execute(command, args);
        }
    }
}
