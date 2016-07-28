﻿// Copyright 2013-2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.File
{
    /// <summary>
    /// Write log events to a disk file.
    /// </summary>
    public sealed class FileSink : ILogEventSink, IDisposable
    {
        const int BytesPerCharacterApproximate = 1;
        readonly TextWriter _output;
        readonly ITextFormatter _textFormatter;
        readonly bool _buffered;
        readonly object _syncRoot = new object();

        /// <summary>Construct a <see cref="FileSink"/>.</summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="textFormatter">Formatter used to convert log events to text.</param>
        /// <param name="fileSizeLimitBytes">The maximum size, in bytes, to which a log file will be allowed to grow.
        /// For unrestricted growth, pass null. The default is 1 GB.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8.</param>
        /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
        /// is false.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>The file will be written using the UTF-8 character set.</remarks>
        /// <exception cref="IOException"></exception>
        public FileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null, bool buffered = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (textFormatter == null) throw new ArgumentNullException(nameof(textFormatter));
            if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative");

            _textFormatter = textFormatter;
            _buffered = buffered;

            TryCreateDirectory(path);

            var file = System.IO.File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var outputWriter = new StreamWriter(file, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (fileSizeLimitBytes != null)
            {
                var initialBytes = file.Length;
                var remainingCharacters = Math.Max(fileSizeLimitBytes.Value - initialBytes, 0L) / BytesPerCharacterApproximate;
                _output = new CharacterCountLimitedTextWriter(outputWriter, remainingCharacters);
            }
            else
            {
                _output = outputWriter;
            }
        }

        static void TryCreateDirectory(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Failed to create directory {0}: {1}", path, ex);
            }
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            lock (_syncRoot)
            {
                _textFormatter.Format(logEvent, _output);
                if (!_buffered)
                    _output.Flush();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources.
        /// </summary>
        public void Dispose() => _output.Dispose();
    }
}
