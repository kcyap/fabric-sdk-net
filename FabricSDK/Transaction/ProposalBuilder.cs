/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using System;
using System.Collections.Generic;
using System.Text;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Common;
using Hyperledger.Fabric.SDK.Protos.Peer;
using Hyperledger.Fabric.SDK.Protos.Peer.FabricProposal;


namespace Hyperledger.Fabric.SDK.Transaction
{
/*
    package org.hyperledger.fabric.sdk.transaction;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Map.Entry;

import com.google.protobuf.ByteString;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.common.Common.HeaderType;
import org.hyperledger.fabric.protos.peer.Chaincode;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeInput;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeInvocationSpec;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeSpec;
import org.hyperledger.fabric.protos.peer.FabricProposal;
import org.hyperledger.fabric.protos.peer.FabricProposal.ChaincodeHeaderExtension;
import org.hyperledger.fabric.protos.peer.FabricProposal.ChaincodeProposalPayload;
import org.hyperledger.fabric.sdk.TransactionRequest;
import org.hyperledger.fabric.sdk.exception.InvalidArgumentException;
import org.hyperledger.fabric.sdk.exception.ProposalException;

import static java.lang.String.format;
import static java.nio.charset.StandardCharsets.UTF_8;
import static org.hyperledger.fabric.sdk.helper.Utils.logString;
import static org.hyperledger.fabric.sdk.transaction.ProtoUtils.createChannelHeader;
import static org.hyperledger.fabric.sdk.transaction.ProtoUtils.getSignatureHeaderAsByteString;
*/
    public class ProposalBuilder
    {

        private static readonly ILog logger = LogProvider.GetLogger(typeof(ProposalBuilder));
        private static bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();

        private Protos.Peer.ChaincodeID chaincodeID;
        protected List<byte[]> argList;
        protected TransactionContext context;
        protected TransactionRequest request;
        protected ChaincodeSpec.Type ccType = ChaincodeSpec.Type.Golang;
        protected Dictionary<string, byte[]> transientMap = null;

        // The channel that is being targeted . note blank string means no specific channel
        private string channelID;

        protected ProposalBuilder()
        {
        }

        public static ProposalBuilder Create()
        {
            return new ProposalBuilder();
        }

        public ProposalBuilder ChaincodeID(Protos.Peer.ChaincodeID chaincodeID)
        {
            this.chaincodeID = chaincodeID;
            return this;
        }

        public ProposalBuilder Args(List<byte[]> argList)
        {
            this.argList = argList;
            return this;
        }
        public ProposalBuilder AddArg(string arg)
        {
            if (this.argList==null)
                argList=new List<byte[]>();
            argList.Add(Encoding.UTF8.GetBytes(arg));
            return this;
        }
        public ProposalBuilder AddArg(byte[] arg)
        {
            if (this.argList == null)
                argList = new List<byte[]>();
            argList.Add(arg);
            return this;
        }

        public void ClearArgs()
        {
            argList = null;
        }
       


        public ProposalBuilder Context(TransactionContext context)
        {
            this.context = context;
            if (null == channelID)
            {
                channelID = context.Channel.Name; //Default to context channel.
            }

            return this;
        }

        public ProposalBuilder Request(TransactionRequest request)
        {
            this.request = request;

            this.chaincodeID = request.ChaincodeID.FabricChaincodeID;

            switch (request.ChaincodeLanguage)
            {
                case TransactionRequest.Type.JAVA:
                    CcType(ChaincodeSpec.Type.Java);
                    break;
                case TransactionRequest.Type.NODE:
                    CcType(ChaincodeSpec.Type.Node);
                    break;
                case TransactionRequest.Type.GO_LANG:
                    CcType(ChaincodeSpec.Type.Golang);
                    break;
                default:
                    throw new InvalidArgumentException("Requested chaincode type is not supported: " + request.ChaincodeLanguage);
            }

            transientMap = request.TransientMap;

            return this;
        }

        public virtual Proposal Build()
        {
            if (request != null && request.NoChannelID)
            {
                channelID = "";
            }

            return CreateFabricProposal(channelID, chaincodeID);
        }

        private Proposal CreateFabricProposal(string channelID, Protos.Peer.ChaincodeID chaincodeID)
        {
            if (null == transientMap)
                transientMap = new Dictionary<string, byte[]>();

            if (IS_DEBUG_LEVEL)
            {
                foreach (KeyValuePair<string, byte[]> tme in transientMap)
                {
                    logger.Debug($"transientMap('{tme.Key.LogString()}', '{Encoding.UTF8.GetString(tme.Value).LogString()}'))");
                }
            }

            ChaincodeHeaderExtension chaincodeHeaderExtension = new ChaincodeHeaderExtension {ChaincodeId = chaincodeID};

            ChannelHeader chainHeader = ProtoUtils.CreateChannelHeader(HeaderType.EndorserTransaction, context.TxID, channelID, context.Epoch, context.FabricTimestamp, chaincodeHeaderExtension, null);

            ChaincodeInvocationSpec chaincodeInvocationSpec = CreateChaincodeInvocationSpec(chaincodeID, ccType);

            ChaincodeProposalPayload payload = new ChaincodeProposalPayload {Input = chaincodeInvocationSpec.SerializeProtoBuf()};
            foreach (KeyValuePair<string, byte[]> pair in transientMap)
            {
                payload.TransientMaps.Add(pair.Key, pair.Value.CloneBytes());
            }

            Header header = new Header {SignatureHeader = ProtoUtils.GetSignatureHeaderAsByteString(context), ChannelHeader = chainHeader.SerializeProtoBuf()};

            return new Proposal {Header = header.SerializeProtoBuf(), Payload = payload.SerializeProtoBuf()};

        }

        private ChaincodeInvocationSpec CreateChaincodeInvocationSpec(Protos.Peer.ChaincodeID chaincodeID, ChaincodeSpec.Type langType)
        {

            List<byte[]> allArgs = new List<byte[]>();

            if (argList != null && argList.Count > 0)
            {
                // If we already have an argList then the Builder subclasses have already set the arguments
                // for chaincodeInput. Accept the list and pass it on to the chaincodeInput builder
                // TODO need to clean this logic up so that common protobuf struct builds are in one place
                allArgs = argList;
            }
            else if (request != null)
            {
                // if argList is empty and we have a Request, build the chaincodeInput args array from the Request args and argbytes lists
                allArgs.Add(Encoding.UTF8.GetBytes(request.Fcn).CloneBytes());
                List<string> args = request.Args;
                if (args != null && args.Count > 0)
                {
                    foreach (string arg in args)
                    {
                        allArgs.Add(Encoding.UTF8.GetBytes(arg).CloneBytes());
                    }
                }

                // TODO currently assume that chaincodeInput args are strings followed by byte[].
                // Either agree with Fabric folks that this will always be the case or modify all Builders to expect
                // a List of Objects and determine if each list item is a string or a byte array
                List<byte[]> argBytes = request.ArgsBytes;
                if (argBytes != null && argBytes.Count > 0)
                {
                    foreach (byte[] arg in argBytes)
                    {
                        allArgs.Add(arg.CloneBytes());
                    }
                }

            }

            if (IS_DEBUG_LEVEL)
            {

                StringBuilder logout = new StringBuilder(1000);

                logout.Append($"ChaincodeInvocationSpec type: {langType.ToString()}, chaincode name: {chaincodeID.Name}, chaincode path: {chaincodeID.Path}, chaincode version: {chaincodeID.Version}");

                String sep = "";
                logout.Append(" args(");

                foreach (byte[] x in allArgs)
                {
                    logout.Append(sep).Append("\"").Append(Encoding.UTF8.GetString(x).LogString()).Append("\"");
                    sep = ", ";

                }

                logout.Append(")");

                logger.Debug(logout.ToString);

            }

            ChaincodeInput chaincodeInput = new ChaincodeInput();
            chaincodeInput.Args.AddRange(allArgs);

            ChaincodeSpec chaincodeSpec = new ChaincodeSpec {type = langType, ChaincodeId = chaincodeID, Input = chaincodeInput};

            return new ChaincodeInvocationSpec {ChaincodeSpec = chaincodeSpec};
        }

        public ProposalBuilder CcType(ChaincodeSpec.Type ccType)
        {
            this.ccType = ccType;
            return this;
        }
    }
}