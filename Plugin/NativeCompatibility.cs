using System;
using System.Security.Cryptography;

namespace SituationalPractice
{
    // Verify the decrypted code we actually use, independent of the EXE's
    // container/signature/other sections. Hook entry ownership is checked separately.
    internal static class NativeCompatibility
    {
        internal sealed class Region
        {
            internal readonly long Rva;
            internal readonly int Length;
            internal readonly string Hash;
            internal Region(long rva,int length,string hash){Rva=rva;Length=length;Hash=hash;}
        }
        // October 13 layout, from the inspected unmodified native memory capture.
        // Contiguous unwind fragments are joined; leaf routines have exact bounds.
        // Call Sheet / Dynasty Hooks redirect two complete five-byte CALLs at
        // 0x7b7e964 (CommitPlay) and 0x35ff6ef (auto-playcall query). Exclude
        // only those instructions; retain every surrounding byte and all of
        // our hook entry checks. Gameplay hooks are unchanged.
        internal static readonly Region[] Regions={
            new Region(0x35bfd70, 514, "9bcceac5bdf0299a678000a45bff93c5cc31b42d5b73f1458c0a79af609cccde"),
            new Region(0x5fde170, 232, "8d7ee05083a27cb5c0bd9102869e26743136ac4b614a8f5fb53b33db0454e94d"),
            new Region(0x2ad8410, 5, "22bdee30f3c1dd6de8568705cde8f9633bc6c8172fa06df18a8a27833dccb072"),
            new Region(0x34864f0, 86, "40fcfa2d50110b60c6d554ec26abbf9d25820ed360b81c54f492ae7c0928adfd"),
            new Region(0x35e0bf0, 445, "9300cda120ac586c107c8e5a640a1a27f6fcecfe5cfc41295911ead612e9b562"),
            new Region(0x35e2110, 368, "bef4c25205a32fd866320561b6af10cb246eae3499e2df2ba478c97a17d73f5d"),
            new Region(0x35fd200, 3356, "0c772e7ddb6adccf826fffa9a86b13e26c4aa19ecf50d1ddecd941e87edf1317"),
            new Region(0x35feb00, 1561, "ce702b8004aabce023a6f6f7608bd9777eddb98de299ab5760fb624a5f2eda13"),
            new Region(0x35ff120, 1487, "2cbdd2d9b512b2b26137b0a3f4bbf735e7150be010b6806ff816edd1aed69ed4"),
            new Region(0x35ff6f4, 486, "b75c9e0f162adec8fdcdb6da06761a84325cf676d539c216401b0d2d6efb9ced"),
            new Region(0x369ed10, 1211, "1e9fc55440fc81b7d20e78fc2a5e2eeb5147aaa4f151b12722ac9476fac58315"),
            new Region(0x377bb40, 136, "58b5bad2cdbd0463c063ca033159db491f7e230b1340b6f9eb87993e79e1d62a"),
            new Region(0x3f079e0, 8, "69e7fce1d328ba00990195f305b2cbfd9d531c2cb2296e3ad2f6c9d09d13f76c"),
            new Region(0x4ff38d0, 1063, "7871d3ab1d41d24c7a1dba46083bc6b90920cd6385bf3f2be98f81636b470898"),
            new Region(0x4ff3d10, 262, "27bbdba8b5598ee7fb9ef7311b0a4364b6a60a9ead8258b77fb08af1f4e38468"),
            new Region(0x4ff3ff0, 783, "8d132f675d22ead45ec87b1977e3db0f8813e63cc6245596abdd725381ce7956"),
            new Region(0x4ff5520, 89, "ced0ab5c750d4eab197f6b378237d7fa95a0319ae04975c6cfede1c7af15feec"),
            new Region(0x5d0e950, 271, "df30a65c0ed3b7445ed82515423e78820b137160b245c2585144f7f793b8cfa6"),
            new Region(0x5d49e30, 516, "898f431b3623a26c95d01849263c81edb0500a3a5f90aae9e8bc22c93070619e"),
            new Region(0x5d59530, 62, "427eb7d7ded1238d359369e66501f52b760c4f5c535a0d6cfbe6d681c4c94fc3"),
            new Region(0x5d59570, 79, "eb36eee844a9e2fd15a637a4c31158d1281be86624703a92ddc7fef486643216"),
            new Region(0x5d595c0, 1156, "6f4b1e7aa31a58c7f6e3f984f2a735a8904fb76bac1e4f8bb67a4c20c0eea86c"),
            new Region(0x5d5aaa0, 1053, "738acdfd283f0db82d82e3eb53e8368fac643e96b64fe3724a0013b12a186434"),
            new Region(0x5d6ca80, 2019, "948c4382fe090dbb7901175e5f47f9c52967ed8512ba7ae3f7f578f2538515aa"),
            new Region(0x5d70850, 7044, "17212aa321020f9b9c9494911593afa9fe0c5815dbab3a1a55d55bd2fd8db301"),
            new Region(0x5f09980, 690, "5f114ba1e0e1ef320957786b178c7b59a417b9081e2f6c51eed49c55ab38686f"),
            new Region(0x7b7e7e0, 388, "c6122c6718de854a9248c59da20d9e00f7bafcbfe92ecaf2a8d7b49f9aab2c78"),
            new Region(0x7b7e969, 44, "f6833726ace23cb818a8b684a30222a1c7f1de3e89d59b363525ea4923123a98"),
            new Region(0x7b99d90, 561, "d7a7682669134a579f6497adc6a7c82f5afac8ae81f09bfca759e320e8deaabe"),
            new Region(0x7babb30, 168, "1c30538c67b9501e8eab43e24d7f1f7d920a2702bcaf5d810af171df7e077d01"),
            new Region(0x7babda0, 103, "0d6b6566e4a0ded35c297957bea100d6519ea18e3a1be3095a2039519641d2cf"),
            new Region(0xae33f50, 16, "231f5cecc61699ca90f5e9acdbc4c47552f66daeb7ef55327cb7574cda9f29e5"),
            new Region(0xbab08c0, 16, "0f023d55ec1805f2c2fbbc1debe1075cfc153ebb81b7814f6f5b9bf7d613146b"),
            new Region(0xbab08d0, 16, "50ec86dca0a9e4d7818f30e38545007351cfd8c06883650b09d93f2789a41c3c"),
            new Region(0xbab08e0, 16, "27d6d18d488331575c4684105e46a16e69d9aa9c7b503590130d7081ec229bae"),
            new Region(0xbab08f0, 16, "c8a6e2851fe308ff9d0e7ce942774e14ca5d4f0655acf8167eae615207bae265"),
            new Region(0x5d59f20, 63, "c5978db6a878cad23828e7979ca2c121c2a65c2de8f9e75809512294e6ebb65c"),
            new Region(0x7bcad50, 72, "c3995397a1556d7bc7d98e5121d66595761e329c7b13a0ac81eb3e4a9f49e856"),
            new Region(0x7c17d20, 8, "60c3957b029169115f5315a518c127beb7315eef963c9c5876a8bbb4e957d577"),
            new Region(0x25a3060, 168, "532f15d1353f4f26cab4cabbd6d044fb27fcd1cb259953136d3d860b3c760686"),
            new Region(0x5d4a660, 596, "89f80498ab9361e6d08b22e6c39d054dafdc77f049fe2a017661f08343883fb1"),
        };
        internal static void Verify(Func<long,int,byte[]> read,long image)
        {
            using(var sha=SHA256.Create())foreach(var region in Regions) {
                byte[] bytes=read(image+region.Rva,region.Length);
                if(bytes==null || bytes.Length!=region.Length)
                    throw new InvalidOperationException("Cannot read required native code at RVA "+region.Rva.ToString("x")+". No hooks installed.");
                string actual=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                if(actual!=region.Hash)
                    throw new InvalidOperationException("Required native code differs at RVA "+region.Rva.ToString("x")+" (expected "+region.Hash+", actual "+actual+"). Waiting for compatible decrypted code; no hooks installed.");
            }
        }
    }
}


