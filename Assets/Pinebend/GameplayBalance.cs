using UnityEngine;

namespace Pinebend
{
    public enum BotDifficulty { Easy, Normal, Hard }

    [CreateAssetMenu(menuName="Pinebend/Gameplay balance")]
    public sealed class GameplayBalance : ScriptableObject
    {
        static GameplayBalance current;
        public static GameplayBalance Current
        {
            get
            {
                if(!current)current=Resources.Load<GameplayBalance>("GameplayBalance");
                if(!current)current=CreateInstance<GameplayBalance>();
                return current;
            }
        }
        public BotDifficulty botDifficulty=BotDifficulty.Normal;
        [Range(1,12)] public float shieldSeconds=5;
        [Range(1,12)] public int shieldGunHits=5;
        [Tooltip("Fixed percentages for every racer. Order follows RallyWeapons.Power; keep total at 100.")]
        public int[] itemPercent={18,16,10,3,8,8,10,12,10,5};

        public float BotSpeed=>botDifficulty switch {BotDifficulty.Easy=>.89f,BotDifficulty.Hard=>1.08f,_=>1};
        public float CornerGrip=>botDifficulty switch {BotDifficulty.Easy=>7.8f,BotDifficulty.Hard=>10.2f,_=>9.2f};
        public float ReactionMin=>botDifficulty switch {BotDifficulty.Easy=>.42f,BotDifficulty.Hard=>.16f,_=>.24f};
        public float ReactionMax=>botDifficulty switch {BotDifficulty.Easy=>.65f,BotDifficulty.Hard=>.27f,_=>.38f};
        public float MissileDelay=>botDifficulty switch {BotDifficulty.Easy=>3.2f,BotDifficulty.Hard=>1.8f,_=>2.3f};

        public bool ValidOdds()
        {
            if(itemPercent==null || itemPercent.Length!=System.Enum.GetValues(typeof(RallyWeapons.Power)).Length)return false;
            int total=0;foreach(int value in itemPercent){if(value<0 || value>100)return false;total+=value;}
            return total==100;
        }
        public RallyWeapons.Power Roll(float unit)
        {
            // Invalid authoring falls back to known fixed odds, never position-dependent rewards.
            int[] odds=ValidOdds() ? itemPercent : new[]{18,16,10,3,8,8,10,12,10,5};
            float ticket=Mathf.Clamp(unit,0,.999999f)*100;
            int threshold=0;
            for(int i=0;i<odds.Length;i++){threshold+=odds[i];if(ticket<threshold)return (RallyWeapons.Power)i;}
            return RallyWeapons.Power.Decoy;
        }
    }
}
