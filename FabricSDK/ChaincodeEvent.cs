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
using Hyperledger.Fabric.SDK.NetExtensions;

namespace Hyperledger.Fabric.SDK
{

    public class ChaincodeEvent
    {
        private readonly byte[] byteString;
        private WeakReference<Protos.Peer.ChaincodeEvent> chaincodeEvent;

        public ChaincodeEvent(byte[] byteString)
        {
            this.byteString = byteString;
        }

        Protos.Peer.ChaincodeEvent Event => byteString.GetOrDeserializeProtoBufWR(ref chaincodeEvent);
        

        /**
         * Get Chaincode event's name;
         *
         * @return Return name;
         */
        public string Name => Event?.EventName;

        /**
         * Get Chaincode identifier.
         *
         * @return The identifier
         */
        public string ChaincodeId => Event?.ChaincodeId;

        /**
         * Get transaction id associated with this event.
         *
         * @return The transactions id.
         */
        public string TxId => Event?.TxId;

        /**
         * Binary data associated with this event.
         *
         * @return binary data set by the chaincode for this event. This may return null.
         */
        public byte[] Payload => Event?.Payload;

    }
}
