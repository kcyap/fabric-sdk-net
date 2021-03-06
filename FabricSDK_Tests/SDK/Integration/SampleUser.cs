/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    public class SampleUser : IUser
    {
        private readonly SampleStore keyValStore;
        private string account;

        private string affiliation;

        private IEnrollment enrollment;

        private string enrollmentSecret;

        /**
         * Save the state of this user to the key value store.
         */
        private bool loading;

        private string mspId;
        private HashSet<string> roles;

        private ICryptoSuite cryptoSuite;

        public SampleUser(string name, string org, SampleStore fs, ICryptoSuite cryptoSuite)
        {
            this.Name = name;
            this.cryptoSuite = cryptoSuite;

            keyValStore = fs;
            Organization = org;
            KeyValStoreName = ToKeyValStoreName(Name, org);
            string memberStr = keyValStore.GetValue(KeyValStoreName);
            if (null == memberStr)
            {
                SaveState();
            }
            else
            {
                RestoreState();
            }
        }


        public string Organization { get; set; }


        public string EnrollmentSecret
        {
            get => enrollmentSecret;
            set
            {
                enrollmentSecret = value;
                SaveState();
            }
        }


        public string KeyValStoreName { get; set; }


        /* Determine if this name has been registered.
        * * @return {
            @code true
        } if registered;

        otherwise {
            @code false
        }.*/

        public bool IsRegistered => !string.IsNullOrEmpty(EnrollmentSecret);

        /**
         * Determine if this name has been enrolled.
         *
         * @return {@code true} if enrolled; otherwise {@code false}.
         */
        public bool IsEnrolled => enrollment != null;


        public string Name { get; }


        public HashSet<string> Roles
        {
            get => roles;
            set
            {
                roles = value;
                SaveState();
            }
        }


        public string Account
        {
            get => account;
            set
            {
                account = value;
                SaveState();
            }
        }


        public string Affiliation
        {
            get => affiliation;
            set
            {
                affiliation = value;
                SaveState();
            }
        }

        [JsonConverter(typeof(InterfaceConverter<IEnrollment, SampleStore.SampleStoreEnrollement>))]
        public IEnrollment Enrollment
        {
            get => enrollment;
            set
            {
                enrollment = value;
                SaveState();
            }
        }

        public string MspId
        {
            get => mspId;
            set
            {
                mspId = value;
                SaveState();
            }
        }

        public static bool IsStored(string name, string org, SampleStore fs)
        {
            return fs.HasValue(ToKeyValStoreName(name, org));
        }

        public void SaveState()
        {
            if (!loading)
            {
                string str = JsonConvert.SerializeObject(this);
                keyValStore.SetValue(KeyValStoreName, str.ToBytes().ToHexString());
            }
        }

        /**
         * Restore the state of this user from the key value store (if found).  If not found, do nothing.
         */
        public SampleUser RestoreState()
        {
            loading = true;
            try
            {
                string memberStr = keyValStore.GetValue(KeyValStoreName);
                if (null != memberStr)
                {
                    JsonConvert.PopulateObject(memberStr.FromHexString().ToUTF8String(), this);
                    return this;
                }
            }
            catch (System.Exception e)
            {
                throw new System.Exception($"Could not restore state of member {Name}", e);
            }
            finally
            {
                loading = false;
            }

            return null;
        }


        public static string ToKeyValStoreName(string name, string org)
        {
            return "user." + name + org;
        }
    }

    public class InterfaceConverter<TInterface, TConcrete> : CustomCreationConverter<TInterface> where TConcrete : TInterface, new()
    {
        public override TInterface Create(Type objectType)
        {
            return new TConcrete();
        }
    }
}