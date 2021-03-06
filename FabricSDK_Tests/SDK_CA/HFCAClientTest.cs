/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.Integration;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class HFCAClientTest
    {
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TEST_ADMIN_PW = "adminpw";
        private static readonly string TEST_ADMIN_ORG = "org1";

        private static CryptoPrimitives crypto;
        private SampleUser admin;

        private SampleStore sampleStore;

        [ClassInitialize]
        public static void SetupBeforeClass(TestContext context)
        {
            try
            {
                crypto = new CryptoPrimitives();
                crypto.Init();
            }
            catch (System.Exception e)
            {
                throw new System.Exception("HFCAClientTest.SetupBeforeClass failed!", e);
            }
        }

        [TestInitialize]
        public void Setup()
        {
            string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");
            if (File.Exists(sampleStoreFile))
            {
                // For testing start fresh
                File.Delete(sampleStoreFile);
            }

            sampleStore = new SampleStore(sampleStoreFile);


            // SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface
            admin = sampleStore.GetMember(TEST_ADMIN_NAME, TEST_ADMIN_ORG);
        }

        [TestMethod]
        public void TestNewInstance()
        {
            HFCAClient memberServices = HFCAClient.Create("http://localhost:99", null);

            Assert.IsNotNull(memberServices);
            Assert.AreSame(typeof(HFCAClient), memberServices.GetType());
        }

        [TestMethod]
        public void TestNewInstanceWithName()
        {
            HFCAClient memberServices = HFCAClient.Create("name", "http://localhost:99", null);

            Assert.IsNotNull(memberServices);
            Assert.AreSame(typeof(HFCAClient), memberServices.GetType());
        }

        [TestMethod]
        public void TestNewInstanceWithNameAndProperties()
        {
            Properties testProps = new Properties();
            HFCAClient memberServices = HFCAClient.Create("name", "http://localhost:99", testProps);

            Assert.IsNotNull(memberServices);
            Assert.AreSame(typeof(HFCAClient), memberServices.GetType());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestNewInstanceNullUrl()
        {
            HFCAClient.Create(null, (Properties) null);
        }

        [TestMethod]
        [ExpectedException(typeof(UriFormatException))]
        public void TestNewInstanceEmptyUrl()
        {
            HFCAClient.Create("", null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "HFCAClient only supports")]
        public void TestNewInstanceBadUrlProto()
        {
            HFCAClient.Create("file://localhost", null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "HFCAClient url does not support path")]
        public void TestNewInstanceBadUrlPath()
        {
            HFCAClient.Create("http://localhost/bad", null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "HFCAClient url needs host")]
        public void TestNewInstanceNoUrlHost()
        {
            HFCAClient.Create("http://:99", null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "HFCAClient url does not support query")]
        public void TestNewInstanceBadUrlQuery()
        {
            HFCAClient.Create("http://localhost?bad", null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "name must not be")]
        public void TestNewInstanceNullName()
        {
            HFCAClient.Create(null, "http://localhost:99", null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "name must not be")]
        public void TestNewInstanceEmptyName()
        {
            HFCAClient.Create("", "http://localhost:99", null);
        }

        [TestMethod]
        public void TestSetCryptoSuite()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);

            CryptoPrimitives testcrypt = new CryptoPrimitives();
            client.CryptoSuite = testcrypt;
            Assert.AreEqual(testcrypt, client.CryptoSuite);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "EntrollmentID cannot be null or empty")]
        public void TestRegisterEnrollmentIdNull()
        {
            RegistrationRequest regreq = new RegistrationRequest("name", "affiliation");
            regreq.EnrollmentID = null;

            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Register(regreq, null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "EntrollmentID cannot be null or empty")]
        public void TestRegisterEnrollmentIdEmpty()
        {
            RegistrationRequest regreq = new RegistrationRequest("name", "affiliation");
            regreq.EnrollmentID = "";

            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Register(regreq, null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Registrar should be a valid member")]
        public void TestRegisterNullRegistrar()
        {
            RegistrationRequest regreq = new RegistrationRequest("name", "affiliation");
            regreq.EnrollmentID = "abc";
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Register(regreq, null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RegistrationException), "Error while registering the user")]
        public void TestRegisterNoServerResponse()
        {
            Properties testProps = new Properties();
            HFCAClient client = HFCAClient.Create("client", "https://localhost:99", testProps);

            CryptoPrimitives testcrypt = new CryptoPrimitives();
            client.CryptoSuite = testcrypt;

            RegistrationRequest regreq = new RegistrationRequest("name", "affiliation");
            client.Register(regreq, admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RegistrationException), "Error while registering the user")]
        public void TestRegisterNoServerResponseAllHostNames()
        {
            Properties testProps = new Properties();
            testProps.Set("allowAllHostNames", "true");
            HFCAClient client = HFCAClient.Create("client", "https://localhost:99", testProps);

            CryptoPrimitives testcrypt = new CryptoPrimitives();
            client.CryptoSuite = testcrypt;

            RegistrationRequest regreq = new RegistrationRequest("name", "affiliation");
            client.Register(regreq, admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Unable to add CA certificate")]
        public void TestRegisterNoServerResponseNoPemFile()
        {
            Properties testProps = new Properties();
            testProps.Set("pemFile", "nofile.pem");
            HFCAClient client = HFCAClient.Create("client", "https://localhost:99", testProps);

            CryptoPrimitives testcrypt = new CryptoPrimitives();
            client.CryptoSuite = testcrypt;

            RegistrationRequest regreq = new RegistrationRequest("name", "affiliation");
            client.Register(regreq, admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "enrollment user is not set")]
        public void TestEnrollmentEmptyUser()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.Enroll("", TEST_ADMIN_PW);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "enrollment user is not set")]
        public void TestEnrollmentNullUser()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.Enroll(null, TEST_ADMIN_PW);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "enrollment secret is not set")]
        public void TestEnrollmentEmptySecret()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.Enroll(TEST_ADMIN_NAME, "");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "enrollment secret is not set")]
        public void TestEnrollmentNullSecret()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.Enroll(TEST_ADMIN_NAME, null);
        }

        // Tests enrollment when no server is available
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to enroll user admin")]
        public void TestEnrollmentNoServerResponse()
        {
            ICryptoSuite cryptoSuite = Factory.Instance.GetCryptoSuite();

            EnrollmentRequest req = new EnrollmentRequest("profile 1", "label 1", null);
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = cryptoSuite;

            client.Enroll(TEST_ADMIN_NAME, TEST_ADMIN_NAME, req);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to enroll user admin")]
        public void TestEnrollmentNoKeyPair()
        {
            ICryptoSuite cryptoSuite = Factory.Instance.GetCryptoSuite();

            EnrollmentRequest req = new EnrollmentRequest("profile 1", "label 1", null);
            req.CSR = "abc";

            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = cryptoSuite;

            client.Enroll(TEST_ADMIN_NAME, TEST_ADMIN_NAME, req);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "reenrollment user is missing")]
        public void TestReenrollNullUser()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Reenroll(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "reenrollment user is not a valid user object")]
        public void TestReenrollNullEnrollment()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            admin.Enrollment = null;
            client.Reenroll(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RevocationException), "Error while revoking")]
        public void TestRevoke1Exception()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            KeyPair keypair = crypto.KeyGen();
            IEnrollment enrollment = new X509Enrollment(keypair, "abc");

            client.Revoke(admin, enrollment, "keyCompromise");
        }

        // revoke1: revoke(User revoker, Enrollment enrollment, String reason)
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "revoker is not set")]
        public void TestRevoke1NullUser()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            KeyPair keypair = crypto.KeyGen();
            IEnrollment enrollment = new X509Enrollment(keypair, "abc");

            client.Revoke(null, enrollment, "keyCompromise");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "revokee enrollment is not set")]
        public void TestRevoke1NullEnrollment()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Revoke(admin, (Enrollment) null, "keyCompromise");
        }

        // revoke2: revoke(User revoker, String revokee, String reason)
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "revoker is not set")]
        public void TestRevoke2NullUser()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Revoke(null, admin.Name, "keyCompromise");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "revokee user is not set")]
        public void TestRevoke2NullEnrollment()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.Revoke(admin, (string) null, "keyCompromise");
        }

        [TestMethod]
        public void TestTLSTrustedCertProperites()
        {
            Properties testprops = new Properties();

            testprops.Set("pemFile", "fixture/testPems/caBundled.pems".Locate() + "," + // has 3 certs
                                     "fixture/testPems/Org1MSP_CA.pem".Locate()); // has 1

            testprops.Set("pemBytes", File.ReadAllText("fixture/testPems/Org2MSP_CA.pem".Locate()));

            CryptoPrimitives cpto = new CryptoPrimitives();
            cpto.Init();

            HFCAClient client = HFCAClient.Create("client", "https://localhost:99", testprops);
            client.CryptoSuite = cpto;
            client.SetUpSSL();
            int count = 0;
            KeyStore trustStore = client.CryptoSuite.Store;
            List<BigInteger> expected = new List<BigInteger>
            {
                new BigInteger("4804555946196630157804911090140692961"),
                new BigInteger("127556113420528788056877188419421545986539833585"),
                new BigInteger("704500179517916368023344392810322275871763581896"),
                new BigInteger("70307443136265237483967001545015671922421894552"),
                new BigInteger("276393268186007733552859577416965113792")
            };
            foreach (X509Certificate2 cert in trustStore.Certificates.Select(a => a.X509Certificate2))
            {
                BigInteger serialNumber = new BigInteger(cert.SerialNumber.FromHexString());
                Assert.IsTrue(expected.Contains(serialNumber), $"Missing certifiate with serial no. {serialNumber}");
                ++count;
            }

            Assert.AreEqual(expected.Count, count, "Number of CA certificates mismatch");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "enrollment is missing")]
        public void TestIdemixNullEnrollment()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.IdemixEnroll(null, "idemixMSP");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "mspID cannot be null or empty")]
        public void TestIdemixMissingMSPID()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            var gen = new ECKeyPairGenerator();
            var keyGenParam = new KeyGenerationParameters(new SecureRandom(), 256);
            gen.Init(keyGenParam);
            KeyPair pair = KeyPair.Create(gen.GenerateKeyPair());
            IEnrollment enrollment = new X509Enrollment(pair, "");
            client.IdemixEnroll(enrollment, null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "enrollment type must be x509")]
        public void TestIdemixWrongEnrollment()
        {
            HFCAClient client = HFCAClient.Create("client", "http://localhost:99", null);
            client.CryptoSuite = crypto;
            IEnrollment enrollment = new IdemixEnrollment(null, null, "mspid", null, null, null, "ou", IdemixRoles.MEMBER);
            client.IdemixEnroll(enrollment, "mspid");
        }

        [TestMethod]
        public void TestAddCAToURL()
        {
            string url = "http://localhost:99";
            HFCAClient client = HFCAClient.Create("ca1", url, null);
            client.CryptoSuite = crypto;
            string url2 = client.AddCAToURL(url);
            Assert.AreEqual(url + "?ca=ca1", url2);
        }
    }
}