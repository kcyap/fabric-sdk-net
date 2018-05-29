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

/**
 * HFCAClient Hyperledger Fabric Certificate Authority Client.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Hyperledger.Fabric_CA.SDK.Logging;
using Hyperledger.Fabric_CA.SDK.Requests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;

namespace Hyperledger.Fabric_CA.SDK
{
    public class HFCAClient
    {
        /**
     * Default profile name.
     */
        public static readonly string DEFAULT_PROFILE_NAME = "";

        /**
     * HFCA_TYPE_PEER indicates that an identity is acting as a peer
     */
        public static readonly string HFCA_TYPE_PEER = "peer";

        /**
     * HFCA_TYPE_ORDERER indicates that an identity is acting as an orderer
     */
        public static readonly string HFCA_TYPE_ORDERER = "orderer";

        /**
     * HFCA_TYPE_CLIENT indicates that an identity is acting as a client
     */
        public static readonly string HFCA_TYPE_CLIENT = "client";

        /**
     * HFCA_TYPE_USER indicates that an identity is acting as a user
     */
        public static readonly string HFCA_TYPE_USER = "user";

        /**
     * HFCA_ATTRIBUTE_HFREGISTRARROLES is an attribute that allows a registrar to manage identities of the specified roles
     */
        public static readonly string HFCA_ATTRIBUTE_HFREGISTRARROLES = "hf.Registrar.Roles";

        /**
     * HFCA_ATTRIBUTE_HFREGISTRARDELEGATEROLES is an attribute that allows a registrar to give the roles specified
     * to a registree for its 'hf.Registrar.Roles' attribute
     */
        public static readonly string HFCA_ATTRIBUTE_HFREGISTRARDELEGATEROLES = "hf.Registrar.DelegateRoles";

        /**
     * HFCA_ATTRIBUTE_HFREGISTRARATTRIBUTES is an attribute that has a list of attributes that the registrar is allowed to register
     * for an identity
     */
        public static readonly string HFCA_ATTRIBUTE_HFREGISTRARATTRIBUTES = "hf.Registrar.Attributes";

        /**
     * HFCA_ATTRIBUTE_HFINTERMEDIATECA is a boolean attribute that allows an identity to enroll as an intermediate CA
     */
        public static readonly string HFCA_ATTRIBUTE_HFINTERMEDIATECA = "hf.IntermediateCA";

        /**
     * HFCA_ATTRIBUTE_HFREVOKER is a boolean attribute that allows an identity to revoker a user and/or certificates
     */
        public static readonly string HFCA_ATTRIBUTE_HFREVOKER = "hf.Revoker";

        /**
     * HFCA_ATTRIBUTE_HFAFFILIATIONMGR is a boolean attribute that allows an identity to manage affiliations
     */
        public static readonly string HFCA_ATTRIBUTE_HFAFFILIATIONMGR = "hf.AffiliationMgr";

        /**
     * HFCA_ATTRIBUTE_HFGENCRL is an attribute that allows an identity to generate a CRL
     */
        public static readonly string HFCA_ATTRIBUTE_HFGENCRL = "hf.GenCRL";

        private static readonly ILog logger = LogProvider.GetLogger(typeof(HFCAClient));

        public static readonly string FABRIC_CA_REQPROP = "caname";
        public static readonly string HFCA_CONTEXT_ROOT = "/api/v1/";

        private static readonly string HFCA_ENROLL = HFCA_CONTEXT_ROOT + "enroll";
        private static readonly string HFCA_REGISTER = HFCA_CONTEXT_ROOT + "register";
        private static readonly string HFCA_REENROLL = HFCA_CONTEXT_ROOT + "reenroll";
        private static readonly string HFCA_REVOKE = HFCA_CONTEXT_ROOT + "revoke";
        private static readonly string HFCA_INFO = HFCA_CONTEXT_ROOT + "cainfo";
        private static readonly string HFCA_GENCRL = HFCA_CONTEXT_ROOT + "gencrl";

        private readonly bool isSSL;


        private readonly Properties properties;

        private readonly string url;

        private X509Store caStore;
        private CryptoPrimitives cryptoPrimitives = null;

        /**
         * HFCAClient constructor
         *
         * @param url        Http URL for the Fabric's certificate authority services endpoint
         * @param properties PEM used for SSL .. not implemented.
         *                   <p>
         *                   Supported properties
         *                   <ul>
         *                   <li>pemFile - File location for x509 pem certificate for SSL.</li>
         *                   <li>allowAllHostNames - boolen(true/false) override certificates CN Host matching -- for development only.</li>
         *                   </ul>
         * @throws MalformedURLException
         */
        public HFCAClient(string caName, string url, Properties properties)
        {
            logger.Debug($"new HFCAClient {url}");
            this.url = url;
            CAName = caName; //name may be null
            Uri purl = null;
            try
            {
                purl = new Uri(url);
            }
            catch (UriFormatException e)
            {
                if (e.Message.Contains("hostname could not be parsed"))
                    throw new IllegalArgumentException("HFCAClient url needs host");
                throw;
            }
            string proto = purl.Scheme;
            if (!"http".Equals(proto) && !"https".Equals(proto))
                throw new IllegalArgumentException("HFCAClient only supports http or https not " + proto);
            string host = purl.Host;
            if (string.IsNullOrEmpty(host))
                throw new IllegalArgumentException("HFCAClient url needs host");
            string path = purl.LocalPath;
            if (!string.IsNullOrEmpty(path) && path!="/")
                throw new IllegalArgumentException("HFCAClient url does not support path portion in url remove path: '" + path + "'.");
            string query = purl.Query;
            if (!string.IsNullOrEmpty(query))
                throw new IllegalArgumentException("HFCAClient url does not support query portion in url remove query: '" + query + "'.");
            isSSL = "https".Equals(proto);
            this.properties = properties?.Clone();
        }

        /**
         * The Certificate Authority name.
         *
         * @return May return null or empty string for default certificate authority.
         */
        public string CAName { get; }

        /**
         * The Status Code level of client, HTTP status codes above this value will return in a
         * exception, otherwise, the status code will be return the status code and appropriate error
         * will be logged.
         *
         * @return statusCode
         */
        public int StatusCode { get; internal set; } = 400;

        public ICryptoSuite CryptoSuite { get; set; }

        public static HFCAClient Create(string url, Properties properties)
        {
            return new HFCAClient(null, url, properties);
        }

        public static HFCAClient Create(string name, string url, Properties properties)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidArgumentException("name must not be null or an empty string.");
            return new HFCAClient(name, url, properties);
        }

        /**
         * Create HFCAClient from a NetworkConfig.CAInfo using default crypto suite.
         *
         * @param caInfo created from NetworkConfig.getOrganizationInfo("org_name").getCertificateAuthorities()
         * @return HFCAClient
         * @throws MalformedURLException
         * @throws InvalidArgumentException
         */

        public static HFCAClient Create(NetworkConfig.CAInfo caInfo)
        {
            try
            {
                return Create(caInfo, HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite());
            }
            catch (Exception e)
            {
                throw new InvalidArgumentException(e);
            }
        }

        /**
         * Create HFCAClient from a NetworkConfig.CAInfo
         *
         * @param caInfo      created from NetworkConfig.getOrganizationInfo("org_name").getCertificateAuthorities()
         * @param cryptoSuite the specific cryptosuite to use.
         * @return HFCAClient
         * @throws MalformedURLException
         * @throws InvalidArgumentException
         */

        public static HFCAClient Create(NetworkConfig.CAInfo caInfo, ICryptoSuite cryptoSuite)
        {
            if (null == caInfo)
                throw new InvalidArgumentException("The caInfo parameter can not be null.");
            if (null == cryptoSuite)
                throw new InvalidArgumentException("The cryptoSuite parameter can not be null.");
            HFCAClient ret = new HFCAClient(caInfo.CAName, caInfo.Url, caInfo.Properties);
            ret.CryptoSuite = cryptoSuite;
            return ret;
        }

        /**
         * Register a user.
         *
         * @param request   Registration request with the following fields: name, role.
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return the enrollment secret.
         * @throws RegistrationException    if registration fails.
         * @throws InvalidArgumentException
         */
        public string Register(RegistrationRequest request, IUser registrar)
        {
            return RegisterAsync(request, registrar).RunAndUnwarp();
        }
        public async Task<string> RegisterAsync(RegistrationRequest request, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            if (string.IsNullOrEmpty(request.EnrollmentID))
                throw new InvalidArgumentException("EntrollmentID cannot be null or empty");
            if (registrar == null)
                throw new InvalidArgumentException("Registrar should be a valid member");
            logger.Debug($"register  url: {url}, registrar: {registrar.Name}");
            SetUpSSL();
            try
            {
                string body = request.ToJson();
                JObject resp = await HttpPostAsync(url + HFCA_REGISTER, body, registrar, token);
                string secret = resp["secret"].Value<string>();
                if (secret == null)
                {
                    throw new Exception("secret was not found in response");
                }

                logger.Debug($"register  url: {url}, registrar: {registrar.Name} done.");
                return secret;
            }
            catch (Exception e)
            {
                RegistrationException registrationException = new RegistrationException($"Error while registering the user {registrar.Name} url: {url}  {e.Message}", e);
                logger.Error(registrationException.Message, registrationException);
                throw registrationException;
            }
        }

        /**
         * Enroll the user with member service
         *
         * @param user   Identity name to enroll
         * @param secret Secret returned via registration
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Enroll(string user, string secret)
        {
            return Enroll(user, secret, new EnrollmentRequest());
        }
        
        public Task<IEnrollment> EnrollAsync(string user, string secret, CancellationToken token = default(CancellationToken))
        {
            return EnrollAsync(user, secret, new EnrollmentRequest(), token);
        }

        /**
         * Enroll the user with member service
         *
         * @param user   Identity name to enroll
         * @param secret Secret returned via registration
         * @param req    Enrollment request with the following fields: hosts, profile, csr, label, keypair
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Enroll(string user, string secret, EnrollmentRequest req)
        {
            return EnrollAsync(user, secret, req).RunAndUnwarp();
        }

        public async Task<IEnrollment> EnrollAsync(string user, string secret, EnrollmentRequest req, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"url: {url} enroll user: {user}");
            if (string.IsNullOrEmpty(user))
                throw new InvalidArgumentException("enrollment user is not set");
            if (string.IsNullOrEmpty(secret))
                throw new InvalidArgumentException("enrollment secret is not set");
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            SetUpSSL();
            try
            {
                string pem = req.CSR;
                AsymmetricAlgorithm keypair = req.KeyPair;
                if (null != pem && keypair == null)
                    throw new InvalidArgumentException("If certificate signing request is supplied the key pair needs to be supplied too.");
                if (keypair == null)
                {
                    logger.Debug("[HFCAClient.enroll] Generating keys...");
                    // generate ECDSA keys: signing and encryption keys
                    keypair = CryptoSuite.KeyGen();
                    logger.Debug("[HFCAClient.enroll] Generating keys...done!");
                }
                if (pem == null)
                    req.CSR = CryptoSuite.GenerateCertificationRequest(user, keypair);
                if (!string.IsNullOrEmpty(CAName))
                    req.CAName = CAName;
                string body = req.ToJson();
                string responseBody = await HttpPostAsync(url + HFCA_ENROLL, body, new NetworkCredential(user, secret), token);
                logger.Debug("response:" + responseBody);
                JObject jsonst = JObject.Parse(responseBody);
                bool success = jsonst["success"].Value<bool>();
                logger.Debug($"[HFCAClient] enroll success:[{success}]");
                if (!success)
                    throw new EnrollmentException($"FabricCA failed enrollment for user {user} response success is false.");
                JObject result = jsonst["result"] as JObject;
                if (result == null)
                    throw new EnrollmentException($"FabricCA failed enrollment for user {user} - response did not contain a result");
                string signedPem = Convert.FromBase64String(result["Cert"].Value<string>()).ToUTF8String();
                logger.Debug($"[HFCAClient] enroll returned pem:[{signedPem}]");
                JArray messages = jsonst["messages"] as JArray;
                if (messages != null && messages.Count > 0)
                {
                    JToken jo = messages[0];
                    string message = $"Enroll request response message [code {jo["code"].Value<int>()}]: {jo["message"].Value<string>()}";
                    logger.Info(message);
                }
                logger.Debug("Enrollment done.");
                return new HFCAEnrollment(keypair, signedPem);
            }
            catch (EnrollmentException ee)
            {
                logger.ErrorException($"url:{url}, user:{user}  error:{ee.Message}", ee);
                throw ee;
            }
            catch (Exception e)
            {
                EnrollmentException ee = new EnrollmentException($"Url:{url}, Failed to enroll user {user}", e);
                logger.ErrorException(e.Message, e);
                throw ee;
            }
        }

        /**
         * Return information on the Fabric Certificate Authority.
         * No credentials are needed for this API.
         *
         * @return {@link HFCAInfo}
         * @throws InfoException
         * @throws InvalidArgumentException
         */
        public HFCAInfo Info()
        {
            return InfoAsync().RunAndUnwarp();
        }
        public async Task<HFCAInfo> InfoAsync(CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"info url:{url}");
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            SetUpSSL();
            try
            {
                JObject body = new JObject();
                if (CAName != null)
                    body.Add(new JProperty(FABRIC_CA_REQPROP, CAName));
                string responseBody = await HttpPostAsync(url + HFCA_INFO, body.ToString(), (NetworkCredential) null, token);
                logger.Debug("response:" + responseBody);
                JObject jsonst = JObject.Parse(responseBody);
                bool success = jsonst["success"].Value<bool>();
                logger.Debug($"[HFCAClient] enroll success:[{success}]");
                if (!success)
                    throw new EnrollmentException($"FabricCA failed info {url}");
                JObject result = jsonst["result"] as JObject;
                if (result == null)
                    throw new InfoException($"FabricCA info error  - response did not contain a result url {url}");
                string caNames = result["CAName"].Value<string>();
                string caChain = result["CAChain"].Value<string>();
                string version = null;
                if (result.ContainsKey("Version"))
                    version = result["Version"].Value<string>();
                return new HFCAInfo(caNames, caChain, version);
            }
            catch (Exception e)
            {
                InfoException ee = new InfoException($"Url:{url}, Failed to get info", e);
                logger.ErrorException(e.Message, e);
                throw ee;
            }
        }

        /**
         * Re-Enroll the user with member service
         *
         * @param user User to be re-enrolled
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Reenroll(IUser user)
        {
            return Reenroll(user, new EnrollmentRequest());
        }
        public Task<IEnrollment> ReenrollAsync(IUser user, CancellationToken token = default(CancellationToken))
        {
            return ReenrollAsync(user, new EnrollmentRequest(), token);
        }

        /**
         * Re-Enroll the user with member service
         *
         * @param user User to be re-enrolled
         * @param req  Enrollment request with the following fields: hosts, profile, csr, label
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Reenroll(IUser user, EnrollmentRequest req)
        {
            return ReenrollAsync(user, req).RunAndUnwarp();
        }
        public async Task<IEnrollment> ReenrollAsync(IUser user, EnrollmentRequest req, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            if (user == null)
                throw new InvalidArgumentException("reenrollment user is missing");
            if (user.Enrollment == null)
                throw new InvalidArgumentException("reenrollment user is not a valid user object");
            logger.Debug($"re-enroll user: {user.Name}, url: {url}");
            try
            {
                SetUpSSL();
                AsymmetricAlgorithm pub = CryptoSuite.BytesToCertificate(user.Enrollment.Cert.ToBytes()).PublicKey.Key;
                AsymmetricAlgorithm priv = user.Enrollment.Key;
                // generate CSR

                string pem = CryptoSuite.GenerateCertificationRequest(user.Name, pub, priv);

                // build request body
                req.CSR = pem;
                if (!string.IsNullOrEmpty(CAName))
                {
                    req.CAName = CAName;
                }

                string body = req.ToJson();

                // build authentication header
                JObject result = await HttpPostAsync(url + HFCA_REENROLL, body, user, token);

                // get new cert from response
                string signedPem = Convert.FromBase64String(result["Cert"].Value<string>()).ToUTF8String();
                logger.Debug($"[HFCAClient] re-enroll returned pem:[{signedPem}]");

                logger.Debug($"reenroll user {user.Name} done.");
                return new HFCAEnrollment(priv, signedPem);
            }
            catch (EnrollmentException ee)
            {
                logger.ErrorException(ee.Message, ee);
                throw ee;
            }
            catch (Exception e)
            {
                EnrollmentException ee = new EnrollmentException($"Failed to re-enroll user {user}", e);
                logger.ErrorException(e.Message, e);
                throw ee;
            }
        }

        /**
         * revoke one enrollment of user
         *
         * @param revoker    admin user who has revoker attribute configured in CA-server
         * @param enrollment the user enrollment to be revoked
         * @param reason     revoke reason, see RFC 5280
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public void Revoke(IUser revoker, IEnrollment enrollment, string reason)
        {
            RevokeAsync(revoker, enrollment, reason).RunAndUnwarp();
        }

        public Task RevokeAsync(IUser revoker, IEnrollment enrollment, string reason, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, enrollment, reason, false, token);
        }

        /**
         * revoke one enrollment of user
         *
         * @param revoker    admin user who has revoker attribute configured in CA-server
         * @param enrollment the user enrollment to be revoked
         * @param reason     revoke reason, see RFC 5280
         * @param genCRL     generate CRL list
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public string Revoke(IUser revoker, IEnrollment enrollment, string reason, bool genCRL)
        {
            return RevokeAsync(revoker, enrollment, reason, genCRL).RunAndUnwarp();
        }
        public Task<string> RevokeAsync(IUser revoker, IEnrollment enrollment, string reason, bool genCRL, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, enrollment, reason, genCRL, token);
        }

        private async Task<string> RevokeInternalAsync(IUser revoker, IEnrollment enrollment, string reason, bool genCRL, CancellationToken token)
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            if (enrollment == null)
                throw new InvalidArgumentException("revokee enrollment is not set");
            if (revoker == null)
                throw new InvalidArgumentException("revoker is not set");
            logger.Debug($"revoke revoker: {revoker.Name}, reason: {reason}, url: {url}x");
            try
            {
                SetUpSSL();
                // get cert from to-be-revoked enrollment
                X509Certificate2 certificate = CryptoSuite.BytesToCertificate(enrollment.Cert.ToBytes());
                Org.BouncyCastle.X509.X509Certificate ncert = DotNetUtilities.FromX509Certificate(certificate);
                // get its serial number
                string serial = ncert.SerialNumber.ToByteArray().ToHexString();
                // get its aki
                // 2.5.29.35 : AuthorityKeyIdentifier
                Asn1OctetString akiOc = ncert.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier.Id);
                string aki = AuthorityKeyIdentifier.GetInstance(akiOc.GetOctets()).GetKeyIdentifier().ToHexString();
                // build request body
                RevocationRequest req = new RevocationRequest(CAName, null, serial, aki, reason, genCRL);
                string body = req.ToJson();
                // send revoke request
                JObject resp = await HttpPostAsync(url + HFCA_REVOKE, body, revoker, token);
                logger.Debug("revoke done");
                if (genCRL)
                {
                    if (!resp.HasValues)
                        throw new RevocationException("Failed to return CRL, revoke response is empty");
                    if (!resp.ContainsKey("CRL"))
                        throw new RevocationException("Failed to return CRL");
                    return resp["CRL"].Value<string>();
                }

                return null;
            }
            catch (NullReferenceException e)
            {
                logger.Error($"Cannot validate certificate. Error is: {e.Message}");
                throw new RevocationException($"Error while revoking cert. {e.Message}", e);
            }
            catch (CertificateException e)
            {
                logger.Error($"Cannot validate certificate. Error is: {e.Message}");
                throw new RevocationException($"Error while revoking cert. {e.Message}", e);
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new RevocationException($"Error while revoking the user. {e.Message}", e);
            }
        }


        /**
         * revoke one user (including his all enrollments)
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param revokee user who is to be revoked
         * @param reason  revoke reason, see RFC 5280
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public void Revoke(IUser revoker, string revokee, string reason)
        {
            RevokeAsync(revoker, revokee, reason).RunAndUnwarp();
        }
        public Task RevokeAsync(IUser revoker, string revokee, string reason, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, revokee, reason, false, token);
        }

        /**
         * revoke one user (including his all enrollments)
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param revokee user who is to be revoked
         * @param reason  revoke reason, see RFC 5280
         * @param genCRL  generate CRL
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public string Revoke(IUser revoker, string revokee, string reason, bool genCRL)
        {
            return RevokeAsync(revoker, revokee, reason, genCRL).RunAndUnwarp();
        }
        public Task<string> RevokeAsync(IUser revoker, string revokee, string reason, bool genCRL, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, revokee, reason, genCRL, token);
        }

        private async Task<string> RevokeInternalAsync(IUser revoker, string revokee, string reason, bool genCRL, CancellationToken token)
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            logger.Debug($"revoke revoker: {revoker}, revokee: {revokee}, reason: {reason}");
            if (string.IsNullOrEmpty(revokee))
                throw new InvalidArgumentException("revokee user is not set");
            if (revoker == null)
                throw new InvalidArgumentException("revoker is not set");
            try
            {
                SetUpSSL();
                // build request body
                RevocationRequest req = new RevocationRequest(CAName, revokee, null, null, reason, genCRL);
                string body = req.ToJson();
                // send revoke request
                JObject resp = await HttpPostAsync(url + HFCA_REVOKE, body, revoker, token);
                logger.Debug($"revoke revokee: {revokee} done.");
                if (genCRL)
                {
                    if (!resp.HasValues)
                        throw new RevocationException("Failed to return CRL, revoke response is empty");
                    if (!resp.ContainsKey("CRL"))
                        throw new RevocationException("Failed to return CRL");
                    return resp["CRL"].Value<string>();
                }
                return null;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new RevocationException($"Error while revoking the user. {e.Message}", e);
            }
        }

        /**
         * revoke one certificate
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param serial  serial number of the certificate to be revoked
         * @param aki     aki of the certificate to be revoke
         * @param reason  revoke reason, see RFC 5280
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public void Revoke(IUser revoker, string serial, string aki, string reason)
        {
            RevokeAsync(revoker, serial, aki, reason).RunAndUnwarp();
        }
        public Task RevokeAsync(IUser revoker, string serial, string aki, string reason, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, serial, aki, reason, false, token);
        }

        /**
         * revoke one enrollment of user
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param serial  serial number of the certificate to be revoked
         * @param aki     aki of the certificate to be revoke
         * @param reason  revoke reason, see RFC 5280
         * @param genCRL  generate CRL list
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public string Revoke(IUser revoker, string serial, string aki, string reason, bool genCRL)
        {
            return RevokeAsync(revoker, serial, aki, reason, genCRL).RunAndUnwarp();
        }
        public Task<string> RevokeAsync(IUser revoker, string serial, string aki, string reason, bool genCRL, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, serial, aki, reason, genCRL, token);
        }

        private async Task<string> RevokeInternalAsync(IUser revoker, string serial, string aki, string reason, bool genCRL, CancellationToken token)
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            if (string.IsNullOrEmpty(serial))
                throw new IllegalArgumentException("Serial number id required to revoke ceritificate");
            if (string.IsNullOrEmpty(aki))
                throw new IllegalArgumentException("AKI is required to revoke certificate");
            if (revoker == null)
                throw new InvalidArgumentException("revoker is not set");
            logger.Debug($"revoke revoker: {revoker.Name}, reason: {reason}, url: {url}");

            try
            {
                SetUpSSL();
                // build request body
                RevocationRequest req = new RevocationRequest(CAName, null, serial, aki, reason, genCRL);
                string body = req.ToJson();
                // send revoke request
                JObject resp = await HttpPostAsync(url + HFCA_REVOKE, body, revoker, token);
                logger.Debug("revoke done");
                if (genCRL)
                {
                    if (!resp.HasValues)
                        throw new RevocationException("Failed to return CRL, revoke response is empty");
                    if (!resp.ContainsKey("CRL"))
                        throw new RevocationException("Failed to return CRL");
                    return resp["CRL"].Value<string>();
                }
                return null;
            }
            catch (CertificateException e)
            {
                logger.ErrorException($"Cannot validate certificate. Error is: {e.Message}", e);
                throw new RevocationException($"Error while revoking cert. {e.Message}", e);
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new RevocationException($"Error while revoking the user. {e.Message}", e);
            }
        }

        /**
         * Generate certificate revocation list.
         *
         * @param registrar     admin user configured in CA-server
         * @param revokedBefore Restrict certificates returned to revoked before this date if not null.
         * @param revokedAfter  Restrict certificates returned to revoked after this date if not null.
         * @param expireBefore  Restrict certificates returned to expired before this date if not null.
         * @param expireAfter   Restrict certificates returned to expired after this date if not null.
         * @throws InvalidArgumentException
         */

        public string GenerateCRL(IUser registrar, DateTime? revokedBefore, DateTime? revokedAfter, DateTime? expireBefore, DateTime? expireAfter)
        {
            return GenerateCRLAsync(registrar, revokedBefore, revokedAfter, expireBefore, expireAfter).RunAndUnwarp();
        }

        public async Task<string> GenerateCRLAsync(IUser registrar, DateTime? revokedBefore, DateTime? revokedAfter, DateTime? expireBefore, DateTime? expireAfter, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            if (registrar == null)
                throw new InvalidArgumentException("registrar is not set");
            try
            {
                SetUpSSL();
                //---------------------------------------
                JObject o = new JObject();
                if (revokedBefore != null)
                    o.Add(new JProperty("revokedBefore", revokedBefore.Value.ToUniversalTime()));
                if (revokedAfter != null)
                    o.Add(new JProperty("revokedAfter", revokedAfter.Value.ToUniversalTime()));
                if (expireBefore != null)
                    o.Add(new JProperty("expireBefore", expireBefore.Value.ToUniversalTime()));
                if (expireAfter != null)
                    o.Add(new JProperty("expireAfter", expireAfter.Value.ToUniversalTime()));
                if (CAName != null)
                    o.Add(new JProperty(FABRIC_CA_REQPROP, CAName));
                string body = o.ToString();
                //---------------------------------------
                // send revoke request
                JObject ret = await HttpPostAsync(url + HFCA_GENCRL, body, registrar, token);
                return ret["CRL"].Value<string>();
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new GenerateCRLException(e.Message, e);
            }
        }

        /**
         * Creates a new HFCA Identity object
         *
         * @param enrollmentID The enrollment ID associated for this identity
         * @return HFCAIdentity object
         * @throws InvalidArgumentException Invalid (null) argument specified
         */

        public HFCAIdentity NewHFCAIdentity(string enrollmentID)
        {
            return new HFCAIdentity(enrollmentID, this);
        }

        /**
         * gets all identities that the registrar is allowed to see
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return the identity that was requested
         * @throws IdentityException        if adding an identity fails.
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public List<HFCAIdentity> GetHFCAIdentities(IUser registrar)
        {
            return GetHFCAIdentitiesAsync(registrar).RunAndUnwarp();
        }

        public async Task<List<HFCAIdentity>> GetHFCAIdentitiesAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (registrar == null)
                throw new InvalidArgumentException("Registrar should be a valid member");
            logger.Debug($"identity  url: {url}, registrar: {registrar.Name}");
            try
            {
                JObject result = await HttpGetAsync(HFCAIdentity.HFCA_IDENTITY, registrar, token);
                List<HFCAIdentity> allIdentities = HFCAIdentity.FromJArray(result["identities"] as JArray);
                logger.Debug($"identity  url: {url}, registrar: {registrar.Name} done.");
                return allIdentities;
            }
            catch (HTTPException e)
            {
                string msg = $"[HTTP Status Code: {e.StatusCode}] - Error while getting all users from url '{url}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.ErrorException(msg, e);
                throw identityException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting all users from url '{url}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.ErrorException(msg, e);
                throw identityException;
            }
        }

        /**
         * @param name Name of the affiliation
         * @return HFCAAffiliation object
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public HFCAAffiliation NewHFCAAffiliation(string name)
        {
            return new HFCAAffiliation(name, this);
        }

        /**
         * gets all affiliations that the registrar is allowed to see
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return The affiliations that were requested
         * @throws AffiliationException     if getting all affiliations fails
         * @throws InvalidArgumentException
         */
        public HFCAAffiliation GetHFCAAffiliations(IUser registrar)
        {
            return GetHFCAAffiliationsAsync(registrar).RunAndUnwarp();
        }

        public async Task<HFCAAffiliation> GetHFCAAffiliationsAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new InvalidArgumentException("Crypto primitives not set.");
            if (registrar == null)
                throw new InvalidArgumentException("Registrar should be a valid member");
            logger.Debug($"affiliations  url: {url}, registrar: {registrar.Name}");
            try
            {
                JObject result = await HttpGetAsync(HFCAAffiliation.HFCA_AFFILIATION, registrar, token);
                HFCAAffiliation affiliations = new HFCAAffiliation(result);
                logger.Debug($"affiliations  url: {url}, registrar: {registrar.Name} done.");
                return affiliations;
            }
            catch (HTTPException e)
            {
                string msg = $"[HTTP Status Code: {e.StatusCode}] - Error while getting all affiliations from url '{url}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.ErrorException(msg, e);
                throw affiliationException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting all affiliations from url '{url}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.ErrorException(msg, e);
                throw affiliationException;
            }
        }

        internal void SetUpSSL()
        {
            if (cryptoPrimitives == null)
            {
                try
                {
                    cryptoPrimitives = new CryptoPrimitives();
                    cryptoPrimitives.Init();
                }
                catch (Exception e)
                {
                    throw new InvalidArgumentException(e);
                }
            }

            if (isSSL && null == caStore)
            {
                if (!properties.Contains("pemBytes") && !properties.Contains("pemFile"))
                    logger.Warn("SSL with no CA certficates in either pemBytes or pemFile");
                try
                {
                    if (properties.Contains("pemBytes"))
                    {
                        byte[] permbytes = properties["pemBytes"].ToBytes();
                        X509Certificate2 cert2 = cryptoPrimitives.BytesToCertificate(permbytes);
                        cryptoPrimitives.AddCACertificateToTrustStore(cert2);
                    }
                    if (properties.Contains("pemFile"))
                    {
                        string pemFile = (string) properties["pemFile"];
                        if (!string.IsNullOrEmpty(pemFile))
                        {
                            Regex pattern = new Regex("[ \t]*,[ \t]*");
                            string[] pems = pattern.Split(pemFile);
                            foreach (string pem in pems)
                            {
                                if (!string.IsNullOrEmpty(pem))
                                {
                                    string fname = Path.GetFullPath(pem);
                                    try
                                    {
                                        byte[] pembytes = File.ReadAllBytes(fname);
                                        List<X509Certificate2> certs = cryptoPrimitives.BytesToCertificates(pembytes);
                                        certs.ForEach(a => cryptoPrimitives.AddCACertificateToTrustStore(a));
                                    }
                                    catch (IOException e)
                                    {
                                        throw new InvalidArgumentException($"Unable to add CA certificate, can't open certificate file {pem}");
                                    }
                                }
                            }
                        }
                    }

                    caStore = cryptoPrimitives.GetTrustStore();
                }
                catch (Exception e)
                {
                    logger.ErrorException(e.Message, e);
                    throw new InvalidArgumentException(e);
                }
            }
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (caStore == null)
                return false;
            foreach (X509Certificate2 cert in caStore.Certificates)
            {
                if (certificate.Subject == cert.Subject && certificate.Issuer == cert.Issuer && certificate.GetCertHashString() == cert.GetCertHashString())
                    return true;
            }
            return false;
        }

        /**
         * Http Post Request.
         *
         * @param url         Target URL to POST to.
         * @param body        Body to be sent with the post.
         * @param credentials Credentials to use for basic auth.
         * @return Body of post returned.
         * @throws Exception
         */

        public virtual string HttpPost(string url, string body, NetworkCredential credentials)
        {
            return HttpPostAsync(url, body, credentials).RunAndUnwarp();
        }

        public async Task<string> HttpPostAsync(string url, string body, NetworkCredential credentials, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"httpPost {url}, body:{body}");
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback += ValidateServerCertificate;
            if (credentials != null)
                handler.Credentials = credentials;
            using (HttpClient client = new HttpClient(handler, true))
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(body, Encoding.UTF8);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                logger.Trace($"httpPost {url}  sending...");
                HttpResponseMessage msg = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
                string result = msg.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                logger.Trace($"httpPost {url}  responseBody {result}");
                int status = (int) msg.StatusCode;
                if (status >= 400)
                {
                    Exception e = new Exception($"POST request to {url}  with request body: {body}, failed with status code: {status}. Response: {result ?? msg.ReasonPhrase}");
                    logger.ErrorException(e.Message, e);
                    throw e;
                }

                logger.Debug($"httpPost Status: {status} returning: {result}");
                return result;
            }
        }

        public virtual JObject HttpPost(string url, string body, IUser registrar)
        {
            return HttpPostAsync(url, body, registrar).RunAndUnwarp();
        }

        public Task<JObject> HttpPostAsync(string url, string body, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(url, "POST", body, registrar, token);
        }

        private async Task<JObject> HttpVerbAsync(string url, string verb, string body, IUser registrar, CancellationToken token)
        {
            string authHTTPCert = GetHTTPAuthCertificate(registrar.Enrollment, body);
            logger.Debug($"http{verb} {url}, body:{body}, authHTTPCert: {authHTTPCert}");
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback += ValidateServerCertificate;
            using (HttpClient client = new HttpClient(handler, true))
            {
                HttpMethod method;
                switch (verb)
                {
                    case "GET":
                        method = HttpMethod.Get;
                        break;
                    case "POST":
                        method = HttpMethod.Post;
                        break;
                    case "PUT":
                        method = HttpMethod.Put;
                        break;
                    case "DELETE":
                        method = HttpMethod.Delete;
                        break;
                    default:
                        method = new HttpMethod(verb);
                        break;
                }

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                request.Headers.Add("Authorization", authHTTPCert);
                if (!string.IsNullOrEmpty(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                }

                HttpResponseMessage msg = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
                return await GetResultAsync(msg, body, verb);
            }
        }

        public JObject HttpGet(string url, IUser registrar)
        {
            return HttpGetAsync(url, registrar).RunAndUnwarp();
        }

        public Task<JObject> HttpGetAsync(string url, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(url, "GET", "", registrar, token);
        }

        public JObject HttpPut(string url, string body, IUser registrar)
        {
            return HttpPutAsync(url, body, registrar).RunAndUnwarp();
        }
        public Task<JObject> HttpPutAsync(string url, string body, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(url, "PUT", body, registrar, token);
        }

        public JObject HttpDelete(string url, IUser registrar)
        {
            return HttpDeleteAsync(url, registrar).RunAndUnwarp();
        }
        public Task<JObject> HttpDeleteAsync(string url, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(url, "DELETE", "", registrar, token);
        }

        private async Task<JObject> GetResultAsync(HttpResponseMessage response, string body, string type)
        {
            int respStatusCode = (int) response.StatusCode;
            logger.Trace($"response status {respStatusCode}, Phrase {response.ReasonPhrase ?? ""}");
            string responseBody = await response.Content.ReadAsStringAsync();
            logger.Trace($"responseBody: {responseBody}");

            // If the status code in the response is greater or equal to the status code set in the client object then an exception will
            // be thrown, otherwise, we continue to read the response and return any error code that is less than 'statusCode'
            if (respStatusCode >= StatusCode)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body}. Response: {responseBody}", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }
            if (responseBody == null)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} with null response body returned.", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }
            logger.Debug("Status: " + respStatusCode);
            JObject jobj = JObject.Parse(responseBody);
            JObject job = new JObject();
            job.Add(new JProperty("statusCode", respStatusCode));
            JArray errors = jobj["errors"] as JArray;
            // If the status code is greater than or equal to 400 but less than or equal to the client status code setting,
            // then encountered an error and we return back the status code, and log the error rather than throwing an exception.
            if (respStatusCode < StatusCode && respStatusCode >= 400)
            {
                if (errors != null && errors.Count > 0)
                {
                    JObject jo = (JObject) errors.First;
                    string errorMsg = $"[HTTP Status Code: {respStatusCode}] - {type} request to {url} failed request body {body} error message: [Error Code {jo["code"].Value<int>()}] - {jo["message"].Value<string>()}";
                    logger.Error(errorMsg);
                }
                return job;
            }
            if (errors != null && errors.Count > 0)
            {
                JObject jo = (JObject) errors.First;
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} error message: [Error Code {jo["code"].Value<int>()}] - {jo["message"].Value<string>()}", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }
            bool success = jobj["success"].Value<bool>();
            if (!success)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} Body of response did not contain success", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            JObject result = jobj["result"] as JObject;
            if (result == null)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} Body of response did not contain result", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            JArray messages = jobj["messages"] as JArray;
            if (messages != null && messages.Count > 0)
            {
                JObject jo = (JObject) messages.First;
                string message = $"{type} request to {url} failed request body {body} response message: [Error Code {jo["code"].Value<int>()}] - {jo["message"].Value<string>()}";
                logger.Info(message);
            }

            // Construct JSON object that contains the result and HTTP status code
            foreach (JProperty prop in result.Children())
                job.Add(prop);
            logger.Debug($"{type} {url}, body:{body} result: {job}");
            return job;
        }

        private string GetHTTPAuthCertificate(IEnrollment enrollment, string body)
        {
            string cert = Convert.ToBase64String(enrollment.Cert.ToBytes());
            body = Convert.ToBase64String(body.ToBytes());
            string signString = body + "." + cert;
            byte[] signature = CryptoSuite.Sign(enrollment.Key, signString.ToBytes());
            return cert + "." + Convert.ToBase64String(signature);
        }


        public string GetURL(string endpoint)
        {
            SetUpSSL();
            string url = this.url + endpoint;
            if (CAName != null)
                url = AddQueryValue(url, "ca", CAName);

            return url;
        }

        private string AddQueryValue(string url, string name, string value)
        {
            if (url.Contains("?"))
                url += "&" + name + "=" + HttpUtility.UrlEncode(value);
            else
                url += "?" + name + "=" + HttpUtility.UrlEncode(value);
            return url;
        }

        public string GetURL(string endpoint, Dictionary<string, string> queryMap)
        {
            SetUpSSL();
            string url = this.url + endpoint;
            if (CAName != null)
                url = AddQueryValue(url, "ca", CAName);
            if (queryMap != null)
            {
                foreach (string key in queryMap.Keys)
                    url = AddQueryValue(url, key, queryMap[key]);
            }

            return url;
        }

        // Convert the identity request to a JSON string
        public string ToJson(JObject toJsonFunc)
        {
            return toJsonFunc.ToString(Formatting.None);
        }
    }
}