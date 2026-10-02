using System;
using System.Collections.Generic;
using System.Linq;

namespace Unice
{
    /// <summary>
    /// The match and its rules. The host runs it (rules, bots, timers) and broadcasts snapshots; everyone else mirrors them
    /// (see Game.Snapshot.cs). A straight port of the web version, so both play the same: keep the numbers and the order of
    /// the checks when changing anything here.
    /// </summary>
    public sealed partial class Game
    {
        // ---------- state ----------
        public Phase phase = Phase.Home;
        /// <summary>Increases every time a new device takes over as host; the highest epoch is the real host.</summary>
        public int epoch;
        /// <summary>Current round (1-based) and how many the match has.</summary>
        public int round, totalRounds = 5;
        /// <summary>Bots the host asked for (more are added when there aren't two characters).</summary>
        public int botCount = 3;
        /// <summary>Seconds the host has been running the match (shown nowhere; sent so guests see it change).</summary>
        public float hostClock;
        /// <summary>Seconds left in the current phase (hide, hunt or result).</summary>
        public float timeLeft;
        /// <summary>Seconds since the round started; the AI and the doll's last giggle are timed with it.</summary>
        public float roundClock;
        public string mapId = MapHouse.Id;

        // host-configurable match settings (defaults match Rules); synced to guests in the snapshot
        public float hideTime = Rules.HideTime, huntTime = Rules.HuntTime, giggleInterval = Rules.GiggleInterval;

        public readonly List<Character> characters = new List<Character>();
        public Character unice, nextUnice;
        public readonly DollState doll = new DollState();
        /// <summary>The last few events (kept short: they travel in every snapshot).</summary>
        public readonly List<GameEvent> events = new List<GameEvent>();
        public int eventSeq;
        /// <summary>How the last round ended, or null while a round is being played.</summary>
        public RoundResult roundResult;
        /// <summary>True on the one device that runs the match.</summary>
        public bool isHost;
        /// <summary>Host: the last action number applied per player, so each action happens once.</summary>
        public readonly Dictionary<string, int> lastActionSeq = new Dictionary<string, int>();

        const int MaxEvents = 10;
        /// <summary>How close (house pixels) a player must be to a spot to hide in it or open it.</summary>
        const float SpotReach = 60;
        /// <summary>Extra reach given to remote players' pass and catch requests, which arrive a little late.</summary>
        const float PassLatencySlack = 20, CatchLatencySlack = 45;

        readonly HiderBot hiderBot;
        readonly UniceBot uniceBot;

        public Game()
        {
            hiderBot = new HiderBot(this);
            uniceBot = new UniceBot(this);
        }

        /// <summary>A round is being played (hiding or hunting).</summary>
        public bool IsLive => phase == Phase.Hide || phase == Phase.Hunt;
        public Character CharacterAt(int index) => index >= 0 && index < characters.Count ? characters[index] : null;

        // ---------- events ----------
        public void AddEvent(GameEventType type, int actor = -1, int target = -1, int value = 0, int x = 0, int y = 0, string text = null)
        {
            eventSeq++;
            events.Add(new GameEvent { sequence = eventSeq, type = type, actor = actor, target = target, value = value, x = x, y = y, text = text });
            if (events.Count > MaxEvents) events.RemoveAt(0);
        }

        // ---------- hiding spots ----------
        /// <summary>The hiding spot within reach of <paramref name="character"/> (the closest), or null.</summary>
        public static Furniture NearestSpot(Character character)
        {
            Furniture nearest = null; float nearestDistance = 34;
            foreach (var spot in Map.Spots)
            {
                float distance = Map.DistanceToFurniture(spot, character.x, character.y);
                if (distance < nearestDistance) { nearestDistance = distance; nearest = spot; }
            }
            return nearest;
        }

        /// <summary>The closest spot no one is in or heading to, within <paramref name="maxDistance"/> and reachable in a straight line.</summary>
        internal Furniture NearestFreeSpot(Character character, float maxDistance)
        {
            Furniture nearest = null; float nearestDistance = maxDistance;
            foreach (var spot in Map.Spots)
            {
                if (spot.occupant != null || (spot.reservedBy != null && spot.reservedBy != character)) continue;
                float distance = Geometry.Distance(spot.standX, spot.standY, character.x, character.y);
                if (distance < nearestDistance && Map.CanWalkStraight(character.x, character.y, spot.standX, spot.standY)) { nearestDistance = distance; nearest = spot; }
            }
            return nearest;
        }

        public void Hide(Character character, Furniture spot)
        {
            if (spot.reservedBy != null && spot.reservedBy != character) spot.reservedBy.brain.targetSpot = null;   // whoever was heading there must pick another
            spot.reservedBy = null; spot.occupant = character;
            character.hidingSpot = spot; character.x = spot.standX; character.y = spot.standY; character.moving = false;
            var brain = character.brain;
            brain.targetSpot = null; brain.previousSpot = spot; brain.path.Clear(); brain.startled = false;
        }

        public static void LeaveSpot(Character character)
        {
            if (character.hidingSpot == null) return;
            character.hidingSpot.occupant = null; character.hidingSpot = null;
        }

        /// <summary>Who would get the doll from <paramref name="giver"/>: the closest visible hider within pass range.</summary>
        public Character PassTarget(Character giver, float extraRange = 0)
        {
            Character nearest = null; float nearestDistance = Rules.PassRange + extraRange;
            foreach (var other in characters)
            {
                if (other == giver || other.IsUnice || other.outOfRound || other.hidingSpot != null) continue;
                float distance = Geometry.Distance(giver, other);
                if (distance < nearestDistance) { nearestDistance = distance; nearest = other; }
            }
            return nearest;
        }

        /// <summary>Whether <paramref name="character"/> can walk right now (the Unice waits outside while the others hide).</summary>
        public bool CanMove(Character character) =>
            IsLive && !character.outOfRound && character.hidingSpot == null && !(character.IsUnice && (phase == Phase.Hide || character.stunTime > 0));

        // ---------- rules (host) ----------
        internal void PassDoll(Character giver, Character receiver)
        {
            doll.holder = receiver; receiver.passCooldown = Rules.PassCooldown; giver.passCooldown = 0;
            AddEvent(GameEventType.Pass, actor: giver.index, target: receiver.index);
        }

        /// <summary>The Unice catches <paramref name="caught"/>: with the doll the round is over, without it they're just out.</summary>
        internal void Capture(Character uniceCharacter, Character caught)
        {
            int x = (int)MathF.Round(caught.x), y = (int)MathF.Round(caught.y);
            LeaveSpot(caught);
            if (caught == doll.holder)
            {
                AddEvent(GameEventType.Catch, actor: uniceCharacter.index, target: caught.index, value: 1, x: x, y: y);
                EndRound(uniceWon: true, caught);
                return;
            }
            caught.outOfRound = true; uniceCharacter.score += 1; uniceCharacter.stunTime = Rules.StunTime;
            var brain = uniceCharacter.brain;
            brain.chaseTarget = null; brain.goal = null; brain.path.Clear(); brain.checkTimer = 0;
            var caughtTarget = caught.brain.targetSpot;
            if (caughtTarget != null && caughtTarget.reservedBy == caught) caughtTarget.reservedBy = null;
            AddEvent(GameEventType.Catch, actor: uniceCharacter.index, target: caught.index, value: 0, x: x, y: y);
        }

        internal void OpenSpot(Character uniceCharacter, Furniture spot)
        {
            spot.checkedAt = roundClock;
            AddEvent(GameEventType.Open, actor: uniceCharacter.index, target: spot.occupant != null ? spot.occupant.index : -1, value: spot.spotIndex);
            if (spot.occupant != null) Capture(uniceCharacter, spot.occupant);
        }

        /// <summary>The doll giggles where its holder is; a bot holder then leaves its spot shortly after.</summary>
        void Giggle()
        {
            var holder = doll.holder;
            doll.giggleTimer = giggleInterval;
            RememberGiggle(holder);
            AddEvent(GameEventType.Giggle, actor: holder.index, x: (int)MathF.Round(holder.x), y: (int)MathF.Round(holder.y));
            if (holder.isBot) holder.brain.leaveSpotAt = roundClock + GameRandom.Range(.3f, 1);
        }

        /// <summary>Anyone can laugh on purpose; the Unice hears it exactly like the doll's own giggle.</summary>
        void Laugh(Character laugher)
        {
            laugher.laughCooldown = Rules.LaughCooldown;
            RememberGiggle(laugher);
            if (doll.holder == laugher) doll.giggleTimer = giggleInterval;
            AddEvent(GameEventType.Giggle, actor: laugher.index, x: (int)MathF.Round(laugher.x), y: (int)MathF.Round(laugher.y));
        }

        void RememberGiggle(Character source)
        {
            doll.hasLastGiggle = true; doll.lastGiggleX = source.x; doll.lastGiggleY = source.y;
            doll.lastGiggleTime = roundClock; doll.lastGiggleSpot = source.hidingSpot;
        }

        /// <summary>Applies an action a player asked for, if the rules allow it right now.</summary>
        public void ApplyAction(Character character, PlayerAction action, int arg)
        {
            if (!IsLive || character.outOfRound) return;
            if (!character.IsUnice)
            {
                if (action == PlayerAction.Hide)
                {
                    var spot = SpotAt(arg);
                    if (spot != null && character.hidingSpot == null && spot.occupant == null && Map.DistanceToFurniture(spot, character.x, character.y) < SpotReach) Hide(character, spot);
                }
                else if (action == PlayerAction.Exit) LeaveSpot(character);
                else if (action == PlayerAction.Pass && doll.holder == character && character.passCooldown <= 0)
                {
                    var receiver = PassTarget(character, PassLatencySlack);
                    if (receiver != null) PassDoll(character, receiver);
                }
                else if (action == PlayerAction.Laugh && character.laughCooldown <= 0) Laugh(character);
            }
            else if (phase == Phase.Hunt && character.stunTime <= 0)
            {
                if (action == PlayerAction.Open)
                {
                    var spot = SpotAt(arg);
                    if (spot != null && Map.DistanceToFurniture(spot, character.x, character.y) < SpotReach) OpenSpot(character, spot);
                }
                else if (action == PlayerAction.Catch)
                {
                    var caught = CharacterAt(arg);
                    if (caught != null && !caught.IsUnice && !caught.outOfRound && caught.hidingSpot == null && Geometry.Distance(character, caught) < Rules.CatchRange + CatchLatencySlack)
                        Capture(character, caught);
                }
            }
        }

        static Furniture SpotAt(int spotIndex) => spotIndex >= 0 && spotIndex < Map.Spots.Length ? Map.Spots[spotIndex] : null;

        // ---------- match and rounds (host) ----------
        /// <summary>A human in the room, as the host knows them when a round starts.</summary>
        public struct Human { public string id, name; public int[] kit; public long joinTime; }

        /// <summary>
        /// Who can be drawn for the Unice or the doll: with two or more people in the room only people,
        /// otherwise (playing alone with bots) everyone, so it is not always the same person.
        /// </summary>
        List<Character> EligibleForDraw(List<Character> candidates)
        {
            var people = candidates.Where(character => !character.isBot).ToList();
            return characters.Count(character => !character.isBot) >= 2 && people.Count > 0 ? people : candidates;
        }

        void AddHuman(Human human)
        {
            if (characters.Count < Rules.MaxPlayers) characters.Add(new Character(characters.Count, human.id, human.name, human.kit, false));
        }

        /// <summary>Starts a match. <paramref name="humans"/> must list this device first, then the others by arrival.</summary>
        public void NewMatch(List<Human> humans)
        {
            if (Map.Id != mapId) Map.Load(Maps.ById(mapId));
            characters.Clear();
            foreach (var human in humans) AddHuman(human);

            // bots take the bot names nobody in the room is using
            var takenNames = new HashSet<string>(characters.Select(character => character.name.ToLowerInvariant()));
            var botNames = Rules.BotNames.Where(name => !takenNames.Contains(name.ToLowerInvariant())).ToList();
            GameRandom.Shuffle(botNames);
            for (int i = 0; (i < botCount || characters.Count < 2) && characters.Count < Rules.MaxPlayers; i++)
                characters.Add(new Character(characters.Count, "b" + i, i < botNames.Count ? botNames[i] : "Robô " + (i + 1), Kit.Random(), true));

            round = 0;
            nextUnice = GameRandom.Pick(EligibleForDraw(characters));
            StartRound(humans);
        }

        /// <summary>Starts the next round: picks up who joined, resets everyone, draws the doll holder and starts the hiding time.</summary>
        public void StartRound(List<Human> humansNow)
        {
            round++;
            foreach (var human in humansNow) if (!characters.Any(character => character.playerId == human.id)) AddHuman(human);
            foreach (var character in characters)
            {
                character.ResetRound(); character.teleportSeq++;
                foreach (var human in humansNow)
                    if (!character.isBot && character.playerId == human.id) { character.name = human.name; character.kit = human.kit; }   // they may have changed their look
            }
            foreach (var spot in Map.Spots) { spot.occupant = null; spot.reservedBy = null; spot.checkedAt = -99; }

            var newUnice = nextUnice != null && characters.Contains(nextUnice) ? nextUnice : GameRandom.Pick(characters);
            newUnice.role = Role.Unice; newUnice.uniceTurns++; unice = newUnice;

            // the others line up at the spawn point
            var hiders = characters.Where(character => character != newUnice).ToList();
            for (int i = 0; i < hiders.Count; i++)
            {
                hiders[i].x = hiders[i].targetX = Map.SpawnX + i * 70;
                hiders[i].y = hiders[i].targetY = Map.SpawnY;
            }
            doll.holder = GameRandom.Pick(EligibleForDraw(hiders)); doll.giggleTimer = giggleInterval; doll.hasLastGiggle = false; doll.lastGiggleSpot = null;
            phase = Phase.Hide; timeLeft = hideTime; roundClock = 0; roundResult = null;
            AddEvent(GameEventType.Start, actor: newUnice.index, target: doll.holder.index, value: round);
        }

        /// <summary>
        /// Scores the round and picks the next Unice: if the Unice won, whoever lost the doll; otherwise the hider who has
        /// been the Unice the fewest times.
        /// </summary>
        void EndRound(bool uniceWon, Character caughtHolder)
        {
            if (!IsLive) return;
            var holder = doll.holder;
            roundResult = new RoundResult { uniceWon = uniceWon };
            if (uniceWon)
            {
                unice.score += 3;
                nextUnice = caughtHolder;
                roundResult.dollHolder = caughtHolder.index;
            }
            else
            {
                foreach (var character in characters) if (!character.IsUnice) character.score += 2;
                holder.score += 1;
                var candidates = EligibleForDraw(characters.Where(character => character != unice).ToList());
                int fewestTurns = candidates.Min(character => character.uniceTurns);
                nextUnice = GameRandom.Pick(candidates.Where(character => character.uniceTurns == fewestTurns).ToList());
                roundResult.dollHolder = holder.index;
                roundResult.holderSpotIndex = holder.hidingSpot != null ? holder.hidingSpot.spotIndex : -1;
            }
            roundResult.nextUnice = nextUnice.index;
            phase = Phase.Result; timeLeft = Rules.ResultTime;
            AddEvent(GameEventType.End, value: uniceWon ? 1 : 0);
        }

        public void BackToLobby()
        {
            phase = Phase.Lobby; characters.Clear(); round = 0; unice = null; doll.holder = null;
            AddEvent(GameEventType.Lobby);
        }

        // ---------- movement ----------
        /// <summary>Moves by (dx, dy), sliding along walls and furniture: each axis only moves if the new spot is free.</summary>
        public static void MoveCharacter(Character character, float dx, float dy)
        {
            float newX = character.x + dx; if (Map.CanStand(newX, character.y)) character.x = newX;
            float newY = character.y + dy; if (Map.CanStand(character.x, newY)) character.y = newY;
        }

        // ---------- host tick ----------
        /// <summary>Host only: one fixed step of the whole match.</summary>
        public void HostStep(float dt, IReadOnlyList<PeerInfo> peers, string myPlayerId, Action<float> moveMyCharacter, Func<List<Human>> humansNow)
        {
            hostClock += dt;
            if (peers != null) ApplyPeerInputs(peers, myPlayerId, dt);
            if (IsLive) { moveMyCharacter?.Invoke(dt); Simulate(dt); }
            else if (phase == Phase.Result)
            {
                timeLeft -= dt;
                if (timeLeft <= 0)
                {
                    if (round >= totalRounds) { phase = Phase.Final; AddEvent(GameEventType.Final); }
                    else StartRound(humansNow());
                }
            }
        }

        /// <summary>One step of a live round: timers, bots, noise, touch catches and the doll's giggle.</summary>
        public void Simulate(float dt)
        {
            roundClock += dt;
            timeLeft -= dt;
            if (phase == Phase.Hide)
            {
                if (timeLeft <= 0) UniceEnters();
            }
            else if (timeLeft <= 0) { EndRound(uniceWon: false, null); return; }

            foreach (var character in characters)
            {
                if (character.passCooldown > 0) character.passCooldown -= dt;
                if (character.laughCooldown > 0) character.laughCooldown -= dt;
                if (character.stunTime > 0) character.stunTime -= dt;
            }

            foreach (var character in characters)
            {
                if (!character.isBot) continue;
                if (character.IsUnice) uniceBot.Update(character, dt); else hiderBot.Update(character, dt);
                if (!IsLive) return;   // a bot just ended the round
                character.loud = !character.IsUnice && character.moving && !character.sneaking;
            }

            // running and talking hiders attract the Unice bot
            foreach (var character in characters)
                if (!character.IsUnice && !character.outOfRound && character.hidingSpot == null && (character.loud || character.talking))
                {
                    character.noiseTimer -= dt;
                    if (character.noiseTimer <= 0) { character.noiseTimer = .42f; uniceBot.HearNoise(character); }
                }

            // the Unice catches whoever it touches (while not stunned or opening a spot)
            if (phase == Phase.Hunt && unice.stunTime <= 0 && unice.brain.checkTimer <= 0)
                foreach (var hider in characters)
                    if (!hider.IsUnice && !hider.outOfRound && hider.hidingSpot == null && Geometry.Distance(unice, hider) < Rules.CatchRange)
                    {
                        Capture(unice, hider);
                        if (!IsLive) return;
                        break;
                    }

            // the doll giggles every few seconds while its holder is hidden
            if (phase == Phase.Hunt && doll.holder.hidingSpot != null)
            {
                doll.giggleTimer -= dt;
                if (doll.giggleTimer <= 0) Giggle();
            }
        }

        /// <summary>The hiding time is over: the Unice appears at the front door, looking in.</summary>
        void UniceEnters()
        {
            phase = Phase.Hunt; timeLeft = huntTime;
            unice.x = Map.Entrance.x; unice.y = Map.Entrance.y; unice.faceX = 0; unice.faceY = -1; unice.teleportSeq++;
            AddEvent(GameEventType.Enter);
        }

        /// <summary>Host only: apply what the other players sent (their position and their last action).</summary>
        public void ApplyPeerInputs(IReadOnlyList<PeerInfo> peers, string myPlayerId, float dt)
        {
            foreach (var character in characters)
            {
                if (character.isBot || character.playerId == myPlayerId) continue;
                PeerInfo peer = null;
                foreach (var candidate in peers) if (candidate.id == character.playerId) { peer = candidate; break; }
                if (peer == null)
                {
                    // the player left: a bot finishes the match in their place
                    character.isBot = true; character.ResetBot();
                    if (IsLive) AddEvent(GameEventType.Left, actor: character.index);
                    continue;
                }

                var presence = peer.presence;
                character.talking = presence.talking;
                int lastSeq = lastActionSeq.TryGetValue(peer.id, out int seq) ? seq : 0;
                if (presence.actionSeq > lastSeq) { lastActionSeq[peer.id] = presence.actionSeq; ApplyAction(character, presence.action, presence.actionArg); }

                // positions sent before the last teleport are stale
                if (presence.hasPosition && presence.teleportSeq >= character.teleportSeq && CanMove(character))
                {
                    float jump = Geometry.Distance(presence.x, presence.y, character.x, character.y);
                    if (jump < 260 && Map.CanStand(presence.x, presence.y))
                    {
                        character.speed += (jump / dt - character.speed) * .25f;
                        character.x = presence.x; character.y = presence.y;
                    }
                    else { character.speed *= .7f; character.teleportSeq++; }   // impossible jump (lag spike): send the player back to where the host has them
                    float faceLength = MathF.Sqrt(presence.faceX * presence.faceX + presence.faceY * presence.faceY);
                    if (faceLength > .1f) { character.faceX = presence.faceX / faceLength; character.faceY = presence.faceY / faceLength; }
                    character.sneaking = presence.sneaking;
                }
                else character.speed *= .7f;
                character.moving = character.speed > 15;
                character.loud = !character.IsUnice && character.moving && !character.sneaking && character.speed > 60;
            }
        }

        /// <summary>The host left: this device continues the match from the last snapshot it received.</summary>
        public void TakeOverAsHost(IReadOnlyList<PeerInfo> peers, Character myCharacter, string myName)
        {
            isHost = true; epoch += 1; lastActionSeq.Clear();
            foreach (var peer in peers) lastActionSeq[peer.id] = peer.presence.actionSeq;   // don't replay actions the old host already applied
            foreach (var character in characters)
            {
                if (character != myCharacter) { character.x = character.targetX; character.y = character.targetY; }
                bool stillHere = peers.Any(peer => peer.id == character.playerId);
                if (!character.isBot && !stillHere) character.isBot = true;
                character.ResetBot();
            }
            foreach (var spot in Map.Spots) spot.reservedBy = null;
            if (doll.hasLastGiggle) doll.lastGiggleSpot = doll.holder != null ? doll.holder.hidingSpot : null;
            AddEvent(GameEventType.Host, text: myName);
        }
    }
}
