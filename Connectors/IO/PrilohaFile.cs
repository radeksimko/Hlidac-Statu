﻿using HlidacStatu.Entities;

namespace HlidacStatu.Connectors.IO
{
    public class PrilohaFile : DistributedFilePath<Smlouva>
    {
        public PrilohaFile()
            : this(Devmasters.Config.GetWebConfigValue("PrilohyDataPath"))
        { }

        public PrilohaFile(string root)
        : base(3, root, (s) => { return Devmasters.Crypto.Hash.ComputeHashToHex(s.Id).Substring(0, 3) + "\\" + s.Id; })
        {
        }
        public override string GetFullDir(Smlouva obj)
        {
            return base.GetFullDir(obj) + obj.Id + "\\";
        }
        public string GetFullPath(Smlouva obj, Smlouva.Priloha priloha)
        {
            return GetFullPath(obj, priloha.odkaz);
        }

        public override string GetFullPath(Smlouva obj, string prilohaUrl)
        {
            if (string.IsNullOrEmpty(prilohaUrl) || obj == null)
                return string.Empty;
            return GetFullDir(obj) + Encode(prilohaUrl);
        }

        public bool ExistLocalCopyOfPriloha(Smlouva obj, Smlouva.Priloha priloha)
        {
            bool weHaveCopy = System.IO.File.Exists(Init.PrilohaLocalCopy.GetFullPath(obj, priloha));
            return weHaveCopy;
        }

        public override string GetRelativeDir(Smlouva obj)
        {
            return base.GetRelativeDir(obj) + obj.Id + "\\";
        }
        public string GetRelativePath(Smlouva obj, Smlouva.Priloha priloha)
        {
            return GetRelativePath(obj, priloha.odkaz);
        }
        public override string GetRelativePath(Smlouva obj, string prilohaUrl)
        {
            if (string.IsNullOrEmpty(prilohaUrl) || obj == null)
                return string.Empty;
            return GetRelativeDir(obj) + Encode(prilohaUrl);

            //return base.GetRelativePath(obj, Devmasters.Crypto.Hash.ComputeHash(prilohaUrl));
        }


        public static string Encode(string prilohaUrl)
        {
            return Devmasters.Crypto.Hash.ComputeHashToHex(prilohaUrl);
            //using (MD5 md5Hash = MD5.Create())
            //{
            //    byte[] md5= md5Hash.ComputeHash(Encoding.UTF8.GetBytes(prilohaUrl));
            //    return System.Convert.ToBase64String(md5.ToString("X")).Replace("=","-");
            //}
        }



    }
}
