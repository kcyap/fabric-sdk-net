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
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Protos.Common;
using Hyperledger.Fabric.SDK.NetExtensions;

namespace Hyperledger.Fabric.SDK
{

    public class ChannelHeaderDeserializer : BaseDeserializer<ChannelHeader>
    {
        public ChannelHeader ChannelHeader => Reference;
        public ChannelHeaderDeserializer(byte[] byteString) : base(byteString)
        {
        }

        public string ChannelId => ChannelHeader?.ChannelId;

        public ulong Epoch => ChannelHeader?.Epoch ?? 0;

        public DateTime? Timestamp => ChannelHeader?.Timestamp;

        public string TxId => ChannelHeader?.TxId;

        public int Type => ChannelHeader?.Type ?? 0;

        public int Version => ChannelHeader?.Version ?? 0;

    }
}

