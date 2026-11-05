using UnityEngine;

// ARcade: a level's own foes, on its prefab (level 1 has none: its bomb guys
// and Slime Boss). EndlessKnightDirector sends these in place of them -
// groups of road foes (RoadEnemy) where the bomb guys came, and its boss
// (RoadBoss) where the Slime Boss did, at the same scores.
//   Level 2: the necromancer (shielded; summons zombies, casts fireballs).
//   Level 3: frogs that leap at her; the giant toad.
//   Level 4: revenants (and skeletons among them); the elder dragon.
public class LevelFoes : MonoBehaviour
{
    public enum Style { Leaper, Shambler, Charger }

    [System.Serializable]
    public class Foe
    {
        /// A copy of it (its arrays shared until replaced: BroodColors' frames).
        public Foe Copy() => (Foe)MemberwiseClone();

        public string name;
        public Style style = Style.Shambler;
        public Sprite[] idle, walk, attack, death;
        [Tooltip("Hit: these frames flickering before it bursts (when it has no death frames).")]
        public Sprite[] hurt;
        public AudioClip attackSound;
        [Tooltip("How loud its attack sound plays.")]
        public float attackVolume = 0.6f;
        [Tooltip("Its attack, hit and death sounds each pitched up or down by up to this at random (the rats: no two squeak alike).")]
        public float soundPitch;
        [Tooltip("Played now and then while it's about (the frogs' ribbits).")]
        public AudioClip[] idleSounds;
        [Tooltip("Its art faces right (else left).")]
        public bool artFacesRight = true;
        [Tooltip("Size against the characters' pixels.")]
        public float scale = 1f;
        [Tooltip("Each one this much of scale, at random (some a little smaller, some a little bigger).")]
        public Vector2 scaleJitter = Vector2.one;
        [Tooltip("As it climbs out of the ground (summoned): one of these (the zombies' moans).")]
        public AudioClip[] riseSounds;
        public float fps = 10f;
        [Tooltip("World units a second.")]
        public float walkSpeed = 0.3f;
        public int damage = 10;
        [Tooltip("Shambler: how far its blow reaches in front of it, world units.")]
        public float reach = 0.5f;
        [Tooltip("Shambler: the attack frame its blow lands on, and for how many frames it can (after that, the swing's spent).")]
        public int hitFrame = 3;
        public int hitFrames = 2;
        [Tooltip("Seconds between attacks.")]
        public float attackCooldown = 2f;
        [Tooltip("Leaper: how near before it leaps, world units; the leap's time and height.")]
        public float leapDistance = 1.1f, leapTime = 0.6f, leapHeight = 0.4f;
        [Tooltip("Leaper: flies up first (leapHeight, in swoopRise seconds), then dives down at her at an angle, diveSpeed world units a second (the birbs).")]
        public bool swoop;
        public float swoopRise = 0.35f, diveSpeed = 3.2f;
        [Tooltip("Leaper: it bursts on touching her - its leap landing on her, or her landing on its head - hurting her as it goes (the frogs).")]
        public bool burstOnContact;
        [Tooltip("Leaper: its crouch before it springs (seconds; the frogs that burst: none), and a hop in its step as it comes (world units high, hops a second).")]
        public float leapWindup = 0.3f;
        public float hopBounce, hopRate = 3f;
        [Tooltip("Charger: how near before it charges, how fast it charges (world units a second), how far past her it runs on.")]
        public float chargeDistance = 1.6f, chargeSpeed = 2.2f, chargeOvershoot = 0.6f;
        [Tooltip("How deep into her body (her art's pixels) it has to come to hurt her.")]
        public float touchPixels = 4f;
        [Tooltip("Its body that hurts her, its art's pixels across about its feet (0: all it's drawn in - a frog's cell has room round it).")]
        public float attackPixels;
        [Tooltip("Shambler: the chance it hops back out of her reach as her swing starts near it, and how far (world units).")]
        [Range(0f, 1f)] public float dodgeChance;
        public float dodgeDistance = 0.45f;
        [Tooltip("A neon bloom along its edges in this colour (clear: none), glowing out this many of its pixels.")]
        public Color bloom = Color.clear;
        public int bloomPixels = 3;
        [Tooltip("With attackPixels: how much of her drawn width is her body it has to come into (from her middle).")]
        [Range(0.1f, 1f)] public float herBodyShare = 0.5f;
        [Tooltip("Blows it takes before it dies (a Holy Slash: all of them); each one flashes it white and splashes its blood.")]
        public int hits = 1;
        [Tooltip("No more than this many of it in a group (0: any number).")]
        public int maxPerGroup;
        public AudioClip hitSound;
        [Tooltip("Played with its hit sound as each blow lands (the goats' blood splash).")]
        public AudioClip impactSound;
        [Tooltip("How loud the impact sound plays, against its hit volume (the goats: half).")]
        public float impactVolume = 1f;
        [Tooltip("A cry as each blow lands, one of these at random (the frogs' loud ribbit).")]
        public AudioClip[] hitCries;
        [Tooltip("Each blow splashes its blood too (a small burst of it, besides the sparks) - and its death's burst this much bigger.")]
        public bool impactGore;
        public float bloodSize = 1f;
        [Tooltip("How loud its hit sound and its death sound play.")]
        public float hitVolume = 0.7f, deathVolume = 0.6f;
        [Tooltip("Played as it hurts her (the goats' charge thudding into her).")]
        public AudioClip hurtPlayerSound;
        [Tooltip("How far a blow that doesn't finish it knocks it back (world units, slid back quickly).")]
        public float knockBack = 0.12f;
        [Tooltip("Her blows reach it this much beyond its drawn body (world units): small ones are easier to hit.")]
        public float hitPadding;
        [Tooltip("Her blows find it only in its body this many of its art's pixels wide, about its feet (0: all it draws) - not out along a long sword.")]
        public float bodyPixels;
        [Tooltip("Her sword reaches it only this close to where she stands (world units, from her middle; 0: wherever the blow does) - the birbs: hit as they dive in, not far out.")]
        public float swordReach;
        [Tooltip("Room kept between it and the next foe on the road (world units, besides their bodies): they don't pile up on each other.")]
        public float padding = 0.12f;
        public float shadowRaise = 2f;
        [Tooltip("A glowing outline round its whole sprite, in this colour (clear: none) - to stand out on a dark road (the revenants' aqua).")]
        public Color outline = Color.clear;
        [Tooltip("The glow round its outline in its own colour (clear: the outline's) - the skeletons' smoky grey.")]
        public Color outlineHalo = Color.clear;
        [Tooltip("Smoke curling up off it (clear: none).")]
        public Color smoke = Color.clear;
        [Tooltip("Sparks rising off it (clear: none) - the skeletons' embers.")]
        public Color sparks = Color.clear;
        [Tooltip("Its halo flickers like fire (a burning aura).")]
        public bool burning;
        [Tooltip("How far its halo reaches round it (1: well out; less: hugging it).")]
        public float auraReach = 1f;
        [Tooltip("Glowing eyes over it, untinted: a sheet sliced as its own, each frame named as its frame is (its _rN_cN), only the eyes on it.")]
        public Sprite[] eyeGlow;
        [Tooltip("Its eye sheet's only used to find its head, frame by frame (for headFire): no glow drawn.")]
        public bool eyeGlowHidden;
        [Tooltip("Comes in swarms: a group of this kind is up to this many of it (any kind with a swarm), close together (0: none).")]
        public int swarm;
        [Tooltip("Pixel fire burning on its head, in this colour (clear: none) - found from eyeGlow's frames.")]
        public Color headFire = Color.clear;
        [Tooltip("Its sprite's colours multiplied by this (white: as drawn) - the skeletons' old, dirty bones.")]
        public Color tint = Color.white;
        [Header("Dying")]
        public bool bones;
        public int boneCount = 9;
        [Tooltip("How far its bones fly.")]
        public float boneSpread = 1f;
        public bool skull = true;
        [Tooltip("Its blood's glowing colour (clear: none).")]
        public Color blood = Color.clear;
        public int bloodCount = 16;
        [Tooltip("Each of her blows' neon sparks off it (FoeFx.Impact): this colour - clear: its blood's (else its outline's).")]
        public Color impactColor = Color.clear;
        [Tooltip("Bones knocked off it by each of her blows (a skeleton's), and its blood: how much of it each blow.")]
        public int hitBones;
        [Tooltip("Drawn this many of her pixels up off the road (art whose feet sit low) - its reflection staying put.")]
        public float liftPixels;
        public int hitBlood = 6;
        [Tooltip("A poof of smoke, and feathers in featherColor (the birbs).")]
        public bool poof, feathers;
        public Color featherColor = Color.white;
        [Tooltip("A blast of its own (the bomb guy's explosion in blastHue) that can hurt her.")]
        public bool blast;
        public float blastHue = 180f;
        public int blastDamage = 15;
        [Tooltip("In reach of her, now and then (this share of its attacks) it blows itself up instead of swinging - its blast in blastHue, hurting her only well inside it.")]
        [Range(0f, 1f)] public float explodeChance;
        [Tooltip("At least one of these in every group of foes that comes.")]
        public bool onePerGroup;
        public AudioClip deathSound;
    }

    [System.Serializable]
    public class BossLook
    {
        public Sprite[] idle, walk, attack, attack2, windup, hurt, death, cast, summon;
        [Tooltip("Its body breathing fire (with the boss's breath frames from its mouth).")]
        public Sprite[] breath;
        [Tooltip("Its blood in this look (the dragon's own colours; clear: the boss's deathBlood / hitBlood).")]
        public Color blood = Color.clear;
        [Tooltip("Its brood (deathSpawn) turned to its colours: a hue turn (degrees), saturation and brightness.")]
        public float broodHue;
        public float broodSaturation = 1f, broodBrightness = 1f;
        [Tooltip("This look's colours from the one art: a hue turn (degrees), saturation and brightness - just the colours within maskWidth degrees of maskHue (0 width: all of them).")]
        public float hue;
        public float saturation = 1f, brightness = 1f, maskHue, maskWidth;
        [Tooltip("Its aura and sparks in this look (clear: the boss's own).")]
        public Color aura = Color.clear, sparks = Color.clear;
        [Header("This look's own ways (0 / clear / -1: the boss's)")]
        [Tooltip("Its fire's hue turn (degrees), a neon glow round the fire, and its lava's colour.")]
        public float breathHue;
        public Color breathGlow = Color.clear, lavaColor = Color.clear;
        [Tooltip("Close enough to claw her, the chance it breathes fire instead (-1: the boss's); seconds between attacks.")]
        public float breathShare = -1f, attackCooldown;
        [Tooltip("Foes it calls up behind her (the final dragon's zombies).")]
        public Foe summonFoe;
        public int maxSummons;
        public float summonCooldown = 7f;
        [Tooltip("Treasure chests falling out of the sky where its body was, one after another, as it dies (the final dragon's); then victorySound.")]
        public int skyChests;
        public AudioClip victorySound;
        [Tooltip("Its health bar's name in this look (empty: the boss's) - the final dragon's LUCIFER.")]
        public string barName;
        [Tooltip("Golden apples thrown out of it as it dies, each healing some of her bar (FAFNIR's), and their pickup sound.")]
        public int goldenApples;
        public AudioClip appleSound;
        [Tooltip("The rocks it throws up in this look (ROCKY's: his own stone's colours; clear: the boss's smashLight / smashDark), and the gems among them (clear: none).")]
        public Color rockLight = Color.clear, rockDark = Color.clear, gemLight = Color.clear, gemDark = Color.clear;
        [Tooltip("Its gems' shape: a cut gem, glowing lava chips, or crystal shards.")]
        public FoeFx.GemKind gemKind;
    }

    [System.Serializable]
    public class Boss
    {
        public string name;
        [Tooltip("The name on its health bar (empty: its name, in capitals).")]
        public string barName;
        [Tooltip("Its name numbered after the first, in Roman numerals (ROCKY, ROCKY II, ROCKY III...).")]
        public bool numberBar;
        [Tooltip("Its blows (claw, breath, shots, beam) this much harder for each one after the first (0.2: the second 20% harder, the third 40%...).")]
        public float damageStep;
        [Tooltip("Where everything hitting it lands - the fairy's ice spikes, its hit sparks and blood: its art's pixels from its feet's pivot, facing right (as castFrom); zero: its middle, its side toward her (the gazer's: its body, off its eye).")]
        public Vector2 hitAt;
        [Tooltip("Its brood (deathSpawn) hopping in with it on its entry leaps, entryBrood more for each one after the first, up to entryBroodMax (the toad's baby frogs).")]
        public int entryBrood, entryBroodMax;
        [Tooltip("The road's swarming foes (level 3's rats) coming in with it: this many for each one of it so far, up to entryRatsMax.")]
        public int entryRats, entryRatsMax;
        [Tooltip("Its colours, one boss after another (cycled).")]
        public BossLook[] looks;
        public bool artFacesRight = true;
        public float scale = 1f;
        public float fps = 10f;
        public int health = 300;
        public float walkSpeed = 0.4f;
        [Tooltip("Each one after the first this much bigger (of the first's size), up to maxSize (0: the director's own).")]
        public float growth, maxSize;
        [Tooltip("Its growth only from the one after this many (level 4: the first four vikings the same, the next four bigger each time).")]
        public int growAfter;
        [Tooltip("It moves in hops (the toad): only on these walk frames, in an arc this high (world units).")]
        public bool hops;
        public int hopFrom = 2, hopTo = 5;
        public float hopHeight = 0.25f;
        [Tooltip("Its hops' frames played at this share of fps (slower: a smoother, weightier hop).")]
        public float hopPace = 0.7f;
        [Tooltip("Its first move: great leaps from where it comes in, up and along, landing in front of her (the toad, meeting her in its arena) - each this high (world units), all of them taking this long (seconds); 0: none.")]
        public float entryLeapHeight, entryLeapTime = 1.2f;
        [Tooltip("How many leaps it takes to reach her.")]
        public int entryLeaps = 1;
        [Tooltip("It keeps this far from her (the necromancer keeps back); 0: comes right up.")]
        public float keepAway;
        [Tooltip("It comes up out of the road in front of her (Gollux), over riseTime seconds - rocks and earth thrown up round it, untouchable till it's out - instead of walking in from the edge.")]
        public bool riseEntry;
        public float riseTime = 1.6f;
        [Tooltip("Played as it starts coming up (riseEntry) - in place of its smash sound as it's out.")]
        public AudioClip riseSound;
        [Tooltip("It flies (the cacodaemon): its feet this high over the road (world units), drifting up floatRise more and back down again every floatPeriod seconds - up there she has to jump to reach it; 0: on the road.")]
        public float floatHeight, floatRise, floatPeriod = 6f;
        public int damage = 15;
        [Tooltip("How far its attack reaches in front of its body, world units.")]
        public float reach = 0.6f;
        [Tooltip("The attack frames its blow is live on.")]
        public int hitFrom = 3, hitTo = 5;
        public float attackCooldown = 2.5f;
        [Tooltip("The blow lands only below this height above the road (world units): jumping clears it.")]
        public float hitHeight = 0.6f;
        [Tooltip("Hits a magic shield takes before it breaks (0: none); a Holy Slash counts two.")]
        public int shieldHits;
        public Color shieldColor = new Color(0.6f, 0.3f, 1f, 0.8f);
        [Tooltip("A glowing outline round it (clear: none), and magic sparks rising off it in sparkColor (clear: none).")]
        public Color outline = Color.clear;
        public Color sparkColor = Color.clear;
        [Tooltip("A neon aura glowing round it, in this colour (clear: none).")]
        public Color aura = Color.clear;
        [Tooltip("How far the aura reaches round it (1: well out; less: hugging it).")]
        public float auraReach = 1f;
        [Tooltip("It never turns round: it faces the side she meets it on for as long as it lives (she can't get past it) - no flipping back and forth as it moves (the dragon).")]
        public bool fixedFacing;
        [Tooltip("It only ever moves toward her - never backs off, never jumps back (the huge dragon).")]
        public bool forwardOnly;
        [Tooltip("Played as a blow lands on the shield (one of shieldHitSounds in turn, if any).")]
        public AudioClip shieldHitSound;
        public AudioClip[] shieldHitSounds;
        [Tooltip("Played as the shield shatters.")]
        public AudioClip shieldBreakSound;
        [Header("Summoning")]
        public Foe summon;
        public int maxSummons;
        public int summonFrame = 10;
        public float summonCooldown = 7f;
        [Header("Fireballs")]
        public Sprite[] projectile;
        public int castFrame = 8;
        public float projectileSpeed = 1.6f;
        public float projectileScale = 0.5f;
        public float projectileHeight = 0.35f;
        public int projectileDamage = 15;
        public float castCooldown = 3.5f;
        [Tooltip("A shotgun burst of this many magic shots instead (1: the single fireball): fanned spreadDegrees wide at her, from castFrom (its art's pixels from its feet's pivot, toward its front), each projectileDamage, in its shield's colour, scaled with it.")]
        public int projectileCount = 1;
        public float spreadDegrees = 30f;
        public Vector2 castFrom;
        [Tooltip("Each shot's own sound as it flies (a little after the one before), and as it bursts.")]
        public AudioClip shotSound, shotImpactSound;
        [Tooltip("Shots fired in volleys of this many (0: all at once), each shot shotGap after the one before, each volley volleyGap after the last and aimed at her anew.")]
        public int volleySize;
        public float shotGap = 0.08f, volleyGap = 0.5f;
        [Tooltip("After summoning, no casting for this long (its summons get to her first).")]
        public float castAfterSummon;
        public float shadowRaise = 2f;
        [Tooltip("The second attack's live frames (attack2: one or the other, at random).")]
        public int hitFrom2 = 3, hitTo2 = 5;
        [Tooltip("Its two attacks in turn, not at random (Gollux).")]
        public bool alternateAttacks;
        [Tooltip("How high and how far back its blow throws her.")]
        public float knockHeight = 0.6f, knockBack = 0.6f;
        [Tooltip("The frame of each attack its fist hits the road on (-1: none) - rocks bursting up there, smashColor's, smashReach (its art's pixels) in front of it, and a heavy shake.")]
        public int smashFrame = -1, smashFrame2 = -1;
        public float smashReach = 30f;
        public Color smashLight = new Color(0.55f, 0.32f, 0.28f), smashDark = new Color(0.22f, 0.13f, 0.14f);
        public AudioClip smashSound;
        [Tooltip("Its death: its hurt frames, then it's gone in an explosion of rocks (smashLight/smashDark) all over its body at once - no blood (Gollux).")]
        public bool rockDeath;
        [Header("Fire breath")]
        [Tooltip("The breath's frames (pivot at their top left: the mouth), turned to breathHue (degrees, from the art's).")]
        public Sprite[] breath;
        public float breathHueTurn;
        [Tooltip("Where its mouth is when breathing, in its art's pixels from its feet's pivot (facing right).")]
        public Vector2 breathMouth;
        [Tooltip("The breath frames that burn, and how far along the road from the mouth it reaches (its art's pixels).")]
        public int breathFrom = 2, breathTo = 5;
        public float breathReach = 140f;
        public int breathDamage = 30;
        [Tooltip("Where the fire burns: along the road from this far past the mouth (its art's pixels) to breathReach, up to breathHeight world units off the road (jump over it).")]
        public float breathNear = 20f, breathHeight = 0.14f;
        [Tooltip("Close enough to claw her, the chance it breathes fire instead.")]
        [Range(0f, 1f)] public float breathShare = 0.35f;
        [Tooltip("Its warning, as it rears up to breathe fire; and the fire's lava (clear: none) dripping off it onto the road.")]
        public AudioClip breathTellSound;
        public Color lavaColor = Color.clear;
        [Header("Sounds")]
        public AudioClip attackSound, windupSound, summonSound, castSound, breathSound;
        [Tooltip("Played as each volley flies (in place of castSound then and each shot's shotSound) - the cacodaemon's fire.")]
        public AudioClip volleySound;
        [Tooltip("Its casts are a beam (the cacodaemon): a blazing line from castFrom at her as she was, beamSeconds long, hurting her once for beamDamage if it crosses her - every castCooldown seconds exactly. Its colour: shieldColor.")]
        public bool beam;
        public int beamDamage = 20;
        public float beamSeconds = 0.7f;
        [Tooltip("The beam's width, her pixels.")]
        public float beamWidth = 5f;
        [Tooltip("As it fires its beam, together: its scream and the beam's own sound.")]
        public AudioClip beamScream, beamSound;
        [Tooltip("Yells and screams, one at random as it attacks (and now and then as it's hit).")]
        public AudioClip[] attackYells;
        [Tooltip("Its own hurt sounds (none: the Slime Boss's).")]
        public AudioClip[] hurtSounds;
        [Tooltip("Played on every blow of hers that lands on it (with its hurt sounds) - its impact.")]
        public AudioClip hitSound;
        [Tooltip("Its blow's burst over her where it lands (the Slime Boss's HIT), over everything.")]
        public GameObject hitFx;
        [Header("Dodging")]
        [Tooltip("Chance a second, while she's close, that it jumps back out of her reach (more when she's just hit it).")]
        public float dodgeChance;
        public float jumpBackDistance = 1f, jumpBackTime = 0.45f, jumpBackHeight = 0.35f;
        [Header("Berserk")]
        [Tooltip("At this share of its health it goes berserk (0: never): a roar, shaking as it grows and flushes red, then a red glow round it, faster in everything.")]
        public float berserkAt;
        public float berserkSpeed = 1.5f, berserkGrowth = 1.15f;
        public Color berserkGlow = new Color(1f, 0.15f, 0.1f, 0.7f);
        public AudioClip roarSound;
        [Header("Hit")]
        [Tooltip("Blood splashed out of it when she hits it (clear: none).")]
        public Color hitBlood = Color.clear;
        [Tooltip("And this many glowing pixel drops of it sprayed out where her blow lands (the toad's neon green); 0: none.")]
        public int hitSpray;
        [Header("Dying")]
        public Color deathBlood = Color.clear;
        public bool deathBones;
        [Tooltip("How big its death burst is: more blood (bigger drops, flung further), more bones, a ring of smoke, and a volley of harmless blasts in deathBlastHue (1: as before, no blasts).")]
        public float deathBurst = 1f;
        public float deathBlastHue = 140f;
        [Tooltip("More bursts of its blood, one after another across its body, all through its dying - every one before its body's gone.")]
        public int deathVolley;
        [Tooltip("Its death blasts are bursts of magic blood, like potions exploding (its shield's colour), not the bomb guy's explosion; with this sound.")]
        public bool magicDeath;
        [Tooltip("Its death bursts are splashes of plain blood (FoeFx.Splash: liquid bursting, heavy drops arcing down, pools on the road - no glow, no flash), each with magicBurstSound now and then - the toad's.")]
        public bool bloodSplash;
        [Tooltip("Dying, it first spews one last gout of its blood from its mouth (breathMouth), its mouth open (its breath frames), for this many seconds.")]
        public float deathSpew;
        [Tooltip("Dying, it first shakes for this many seconds, crying out (deathCry: the toad's ribbit), then bursts (its deathSound then).")]
        public float deathShake;
        public AudioClip deathCry;
        [Tooltip("Its gems shoot straight up in a fountain (the toad's), not flung wide.")]
        public bool gemFountain;
        [Tooltip("Its brood flung all over (both ways, near and far) - not just away from her.")]
        public bool broodEverywhere;
        [Header("Treasure")]
        [Tooltip("A special treasure it leaves (a big spinning gem, glowing, sparkling), worth treasureValue.")]
        public GameObject treasurePrefab;
        public int treasureValue = 25000;
        public float treasureScale = 2.5f;
        public Color treasureGlow = new Color(1f, 0.85f, 0.3f, 0.8f);
        [Tooltip("Played as she picks the treasure up (a cha-ching).")]
        public AudioClip treasureSound;
        public AudioClip magicBurstSound;
        [Tooltip("Thrown out of it as it dies, landing and coming for her (the toad's baby frogs).")]
        public Foe deathSpawn;
        public int deathSpawnCount;
        [Tooltip("Its own death cry (none: the Slime Boss's).")]
        public AudioClip deathSound;
        [Tooltip("More screams after it, one after another, as it dies.")]
        public AudioClip[] deathScreams;
        public float screamGap = 0.55f;
        [Tooltip("Played as each of its gems bursts out.")]
        public AudioClip gemSound;
        [Tooltip("Leaves the treasure chest (else just its gems).")]
        public bool dropChest = true;
        [Tooltip("Gems burst out of it as it dies, each worth gemValue points.")]
        public GameObject gemPrefab;
        [Tooltip("More kinds among its gems (emeralds, diamonds...), mixed in with gemPrefab.")]
        public GameObject[] gemPrefabs;
        public int gemCount, gemValue = 500;
        [Tooltip("Its gems this much bigger (the dragon's).")]
        public float gemScale = 1f;
        [Header("Food")]
        [Tooltip("Food thrown out of it as it dies (one set for each of it in turn, round again after the last): eaten, a big heal, and she grows big (PlayerBuffs.Big) for foodBigSeconds.")]
        public FoodSet[] foods;
        public int foodCount = 4;
        public float foodScale = 0.32f, foodBigSeconds = 30f;
        [Tooltip("Of her health bar, healed by each one.")]
        [Range(0f, 1f)] public float foodHeal = 0.35f;
        [Tooltip("As she eats it; as she grows big from it (eaten at full health); as she shrinks back.")]
        public AudioClip foodSound, growSound, shrinkSound;
        [Tooltip("Her blows on it while she's big: the sword's and the Holy Slash's damage times these.")]
        public float bigHitMultiplier = 1f, bigHolyMultiplier = 1f;
    }

    [System.Serializable]
    public class FoodSet
    {
        public string name;
        public Sprite[] items;
    }

    [Tooltip("Of each group: the chance it's a bomb guy instead (level 1 keeps its bomb guys, birbs among them); during a boss fight it's always the foes.")]
    [Range(0f, 1f)] public float bombShare;
    [Tooltip("Road foes (one picked per group, by weight).")]
    public Foe[] road;
    public float[] roadWeights;
    [Tooltip("Up to this many come in a group.")]
    public int groupMax = 3;
    [Tooltip("Frames between the foes in a group.")]
    public float groupSpacing = 14f;
    public Boss boss;
    [Tooltip("A second boss taking turns with the first (level 4: the dragon, then Fantasy #25, then the dragon...).")]
    public Boss altBoss;
    [Tooltip("The second boss comes this many times, then the first once (0: they take turns) - level 4: four vikings, then the dragon.")]
    public int altRun;
}
