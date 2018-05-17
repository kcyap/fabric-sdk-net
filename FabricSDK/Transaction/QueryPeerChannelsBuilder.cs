/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

namespace Hyperledger.Fabric.SDK.Transaction
{

    public class QueryPeerChannelsBuilder : CSCCProposalBuilder
    {

        private QueryPeerChannelsBuilder()
        {
            AddArg("GetChannels");
        }

        public new QueryPeerChannelsBuilder Context(TransactionContext context)
        {
            base.Context(context);
            return this;
        }

        public new static QueryPeerChannelsBuilder Create()
        {
            return new QueryPeerChannelsBuilder();
        }
    }
}

