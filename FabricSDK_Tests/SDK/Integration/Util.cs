/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.Tests.Helper;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    public static class Util
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Util));
        /**
     * Private constructor to prevent instantiation.
     */


        /**
     * Generate a targz inputstream from source folder.
     *
     * @param src        Source location
     * @param pathPrefix prefix to add to the all files found.
     * @return return inputstream.
     * @throws IOException
     */
        public static Stream GenerateTarGzInputStream(string src, string pathPrefix)
        {
            MemoryStream bos = new MemoryStream();

            string sourcePath = src;
            using (var writer = WriterFactory.Open(bos, ArchiveType.Tar, CompressionType.GZip))
            {
                string[] files = Directory.GetFiles(src, "*", SearchOption.AllDirectories).ToArray();
                foreach (string childPath in files)
                {
                    string relativePath = childPath.Substring(sourcePath.Length + 1);
                    if (pathPrefix != null)
                        relativePath = Path.Combine(pathPrefix, relativePath);
                    writer.Write(relativePath, File.OpenRead(childPath));
                }
            }

            bos.Flush();
            bos.Position = 0;
            return bos;
        }


        public static string FindFileSk(string directorys)
        {
            string[] matches = Directory.EnumerateFiles(directorys.Locate()).Where(a => a.EndsWith("_sk")).ToArray();
            if (null == matches)
                throw new System.Exception($"Matches returned null does {directorys} directory exist?");
            if (matches.Length != 1)
                throw new SystemException($"Expected in {directorys} only 1 sk file but found {matches.Length}");
            return matches[0];
        }

        public static void COut(string format, params object[] args)
        {
            logger.Debug(string.Format(format, args));
        }

        public static T Get<T>(this TaskCompletionSource<T> tco, int timeoutinmilliseconds)
        {
            T result = default(T);
            bool failed = true;
            Task.Run(async () =>
            {
                using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                {
                    var completedTask = await Task.WhenAny(tco.Task, Task.Delay(timeoutinmilliseconds, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
                    if (completedTask == tco.Task)
                    {
                        timeoutCancellationTokenSource.Cancel();
                        result = tco.Task.Result;
                        failed = false;
                    }
                }
            }).Wait();
            if (failed)
                throw new TimeoutException("The operation has timed out.");
            return result;
        }

        public static T Get<T>(this TaskCompletionSource<T> tco)
        {
            return tco.Task.GetAwaiter().GetResult();
        }
    }
}