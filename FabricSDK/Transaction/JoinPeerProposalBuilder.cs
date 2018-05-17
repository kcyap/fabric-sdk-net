/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *        http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System.Collections.Generic;
using System.Text;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Common;

namespace Hyperledger.Fabric.SDK.Transaction
{
    public class JoinPeerProposalBuilder : CSCCProposalBuilder
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(JoinPeerProposalBuilder));
        
        public JoinPeerProposalBuilder GenesisBlock(Block genesisBlock)
        {
            if (genesisBlock == null) {
                ProposalException exp = new ProposalException("No genesis block for Join proposal.");
                logger.ErrorException(exp.Message, exp);
                throw exp;
            }
            AddArg("JoinChain");
            AddArg(genesisBlock.SerializeProtoBuf());
            return this;
        }

        private JoinPeerProposalBuilder() {

        }

        public new JoinPeerProposalBuilder Context(TransactionContext context)
        {
            base.Context(context);
            return this;
        }

        public new static JoinPeerProposalBuilder Create() {
            return new JoinPeerProposalBuilder();
        }

    }
}

