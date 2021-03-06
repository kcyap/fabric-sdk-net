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

namespace Hyperledger.Fabric_CA.SDK
{
    /**
     * Fabric Certificate authority information
     * Contains information for the Fabric certificate authority
     */
    public class HFCAInfo
    {
        // Contains server/ca information
        public HFCAInfo(string caName, string caChain, string version)
        {
            CAName = caName;
            CACertificateChain = caChain;
            Version = version;
            IdemixIssuerPublicKey = "";
            IdemixIssuerRevocationPublicKey = "";
        }

        // Contains server/ca information
        public HFCAInfo(string caName, string caChain, string version, string issuerPublicKey, string issuerRevocationPublicKey)
        {
            CAName = caName;
            CACertificateChain = caChain;
            Version = version;
            IdemixIssuerPublicKey = issuerPublicKey;
            IdemixIssuerRevocationPublicKey = issuerRevocationPublicKey;
        }
        /**
         * The CAName for the Fabric Certificate Authority.
         *
         * @return The CA Name.
         */

        public string CAName { get; }


        /**
         * The Certificate Authority's Certificate Chain.
         *
         * @return Certificate Chain in X509 PEM format.
         */

        public string CACertificateChain { get; }

        /**
         * Version of Fabric CA server
         *
         * @return Version of Fabric CA server, value will be
         * null for Fabric CA prior to 1.1.0
         */

        public string Version { get; }
        /**
         * Get the idemix issuer public key.
         *
         * @return The idemix issuer public key, or null if not supported by
         * this version of idemix.
         */
        public string IdemixIssuerPublicKey { get; }
        /**
         * Get the idemix issuer revocation public key.
         *
         * @return The idemix issuer revocation public key, or null if not supported by
         * this version of idemix.
         */
        public string IdemixIssuerRevocationPublicKey { get; }
    }
}