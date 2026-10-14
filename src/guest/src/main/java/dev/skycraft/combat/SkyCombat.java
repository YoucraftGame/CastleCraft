package dev.skycraft.combat;

import dev.skycraft.SkyCraft;
import dev.skycraft.link.Proto;
import dev.skycraft.link.SkyLink;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import net.fabricmc.fabric.api.object.builder.v1.entity.FabricDefaultAttributeRegistry;
import net.minecraft.core.Registry;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.core.registries.Registries;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.Identifier;
import net.minecraft.resources.ResourceKey;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.damagesource.DamageSources;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.MobCategory;
import org.jspecify.annotations.Nullable;

/**
 * Combat between the Minecraft player and Skyrim actors, server side.
 *
 * <p>Every Skyrim actor near the player gets an invisible {@link SkyrimActorEntity} at its exact
 * position. Minecraft weapons hit those like any mob; the resulting damage is sent to Skyrim, which
 * applies it to the real actor (scaled by level) and makes it fight back. Skyrim's hits on the player
 * come back as Minecraft damage from the attacker's stand-in, so armor, shields, knockback, hurt
 * sounds and death all work the Minecraft way.
 */
public final class SkyCombat {
	public static final ResourceKey<EntityType<?>> SKYRIM_ACTOR_KEY =
		ResourceKey.create(Registries.ENTITY_TYPE, Identifier.fromNamespaceAndPath(SkyCraft.MOD_ID, "skyrim_actor"));
	public static final EntityType<SkyrimActorEntity> SKYRIM_ACTOR = Registry.register(
		BuiltInRegistries.ENTITY_TYPE,
		SKYRIM_ACTOR_KEY,
		EntityType.Builder.<SkyrimActorEntity>of(SkyrimActorEntity::new, MobCategory.MISC)
			.sized(0.6F, 1.8F)
			.noSave()
			.noSummon()
			.noLootTable()
			.clientTrackingRange(10)
			.updateInterval(1)
			.build(SKYRIM_ACTOR_KEY)
	);

	/** Skyrim damage is divided by this for Minecraft (a 15-damage bandit swing = 3 = 1.5 hearts). */
	public static final float SKYRIM_TO_MC_DAMAGE = 5.0F;

	private static final Map<Integer, SkyrimActorEntity> PROXIES = new HashMap<>();
	private static final List<SkyLink.Actor> ACTORS = new ArrayList<>();
    // Target goals are restored on unlink; these rules never apply to an ordinary Minecraft save.
    private static final Map<net.minecraft.world.entity.Mob, java.util.List<net.minecraft.world.entity.ai.goal.WrappedGoal>> BRIDGED_MOBS = new java.util.WeakHashMap<>();
    private static int mobScan;
    private static int damageEvents;
    private record PendingHit(int actor, CastleHitAccumulator.Hit hit) {}
    private static final java.util.ArrayDeque<PendingHit> OUTBOUND_HITS = new java.util.ArrayDeque<>();
    private static ServerLevel outboundLevel;
    private static MinecraftServer checkedServer;
    private static boolean checkedMirror;
    private static ServerLevel bridgeLevel;
    private static volatile net.minecraft.world.level.Level clientBridgeLevel;
    private static final int ACTOR_RECTANGULAR_FOOTPRINT = 1 << 4;

    private static final class CastleTargetGoal extends net.minecraft.world.entity.ai.goal.target.NearestAttackableTargetGoal<SkyrimActorEntity> {
        private int unseenTicks;
        CastleTargetGoal(net.minecraft.world.entity.Mob mob) {
            super(mob, SkyrimActorEntity.class, 5, true, false,
                (entity, level) -> entity instanceof SkyrimActorEntity actor && actor.castleHostile);
            this.targetConditions.ignoreInvisibilityTesting();
        }
        @Override protected void findTarget() { this.target = nearestEnemy(mob, getFollowDistance()); }
        @Override public boolean canUse() { return isBridgeLevel(mob.level()) && super.canUse(); }
        @Override public void start() { unseenTicks = 0; super.start(); }
        @Override public boolean canContinueToUse() {
            if (!isBridgeLevel(mob.level()) || !(mob.getTarget() instanceof SkyrimActorEntity actor)
                || !actor.castleHostile || !actor.isAlive() || !mob.canAttack(actor)) return false;
            double range = getFollowDistance();
            if (actor.getBoundingBox().distanceToSqr(mob.getBoundingBox()) > range * range) return false;
            // Vanilla uses center distance here, which drops very large bosses even when a
            // mob is touching the near body surface. Keep vanilla's brief sight-loss grace.
            if (mob.hasLineOfSight(actor)) unseenTicks = 0;
            else if (++unseenTicks > unseenMemoryTicks) return false;
            return true;
        }
    }

    private SkyCombat() {}

    public static void init() {
        registerActorAttributes();
        net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents.SERVER_STARTING.register(server->{registerActorAttributes();});
        ServerTickEvents.START_SERVER_TICK.register(SkyCombat::beforeServerTick);
        ServerTickEvents.END_SERVER_TICK.register(SkyCombat::serverTick);
        net.fabricmc.fabric.api.entity.event.v1.ServerLivingEntityEvents.ALLOW_DAMAGE.register((victim, source, amount) -> {
            if(dev.skycraft.world.BridgeSimulation.cinematic(victim))return false;
            return !(victim instanceof net.minecraft.world.entity.player.Player
                && isBridgeLevel(victim.level()) && source.getEntity() instanceof net.minecraft.world.entity.Mob mob
                && isCastleAlly(mob));
        });
    }

    private static void registerActorAttributes(){
        var actorAttributes=LivingEntity.createLivingAttributes()
            .add(net.minecraft.world.entity.ai.attributes.Attributes.ATTACK_DAMAGE,1)
            .add(net.minecraft.world.entity.ai.attributes.Attributes.ATTACK_KNOCKBACK,0);
        // Copied equipment needs the real registered spell attributes to apply its modifiers.
        net.minecraft.core.registries.BuiltInRegistries.ATTRIBUTE.listElements().filter(a->a.key().identifier().getNamespace().equals("spell_power")).forEach(actorAttributes::add);
        FabricDefaultAttributeRegistry.register(SKYRIM_ACTOR,actorAttributes);
    }

    public static boolean isBridgeLevel(net.minecraft.world.level.Level level) {
        if (!SkyLink.active() || !(level instanceof ServerLevel serverLevel)) return false;
        MinecraftServer server = serverLevel.getServer();
        if (server != checkedServer) {
            checkedServer = server;
            var root = server.getWorldPath(net.minecraft.world.level.storage.LevelResource.ROOT).toAbsolutePath().normalize();
            checkedMirror = root.getFileName() != null && SkyCraft.WORLD_NAME.equals(root.getFileName().toString())
                && server.isSingleplayer();
        }
        return checkedMirror && level.dimension() == net.minecraft.world.level.Level.OVERWORLD;
    }

    /** The render thread supplies only the exact locally-owned mirror level. */
    public static void setClientBridgeLevel(net.minecraft.world.level.@Nullable Level level) {
        clientBridgeLevel = level;
    }

    /** Geometry is world-scoped too: cached native terrain must not leak into ordinary saves. */
    public static boolean isBridgeGeometry(net.minecraft.world.level.BlockGetter view) {
        if (!SkyLink.active()) return false;
        net.minecraft.world.level.Level level = view instanceof net.minecraft.world.level.Level direct ? direct
            : view instanceof dev.skycraft.mixin.PathNavigationRegionAccessor region ? region.skycraft$level() : null;
        return level != null && (level == clientBridgeLevel || isBridgeLevel(level));
    }

    public static @Nullable SkyrimActorEntity proxy(int formId) { return PROXIES.get(formId); }

    public static boolean isCastleAlly(net.minecraft.world.entity.Mob mob) {
        return mob instanceof net.minecraft.world.entity.monster.Enemy
            || mob instanceof net.minecraft.world.entity.animal.golem.IronGolem
            || mob.getType().getCategory()==MobCategory.MONSTER;
    }

    public static void hurtMob(ServerPlayer owner,int id,java.util.UUID uuid,int kind,float nativeDamage,int attackerId,int epoch){
        var level=owner.level();var victim=level.getEntity(id);
        if(!isBridgeLevel(level)||epoch!=dev.skycraft.world.SkyCollision.epoch()||!(victim instanceof net.minecraft.world.entity.Mob mob)
            ||!mob.getUUID().equals(uuid)||!mob.isAlive()||!isCastleAlly(mob)||dev.skycraft.world.BridgeSimulation.paused(mob)
            ||dev.skycraft.world.BridgeSimulation.cinematic(mob)||!Float.isFinite(nativeDamage)||nativeDamage<=0)return;
        var attacker=PROXIES.get(attackerId);var sources=level.damageSources();
        var source=attacker==null?sources.generic():(kind&1)!=0?sources.mobProjectile(attacker,attacker):sources.mobAttack(attacker);
        float damage=nativeDamage/SKYRIM_TO_MC_DAMAGE;
        boolean hit=mob.hurtServer(level,source,damage);
        if(hit&&attacker!=null&&(kind&4)!=0)mob.knockback(.5,attacker.getX()-mob.getX(),attacker.getZ()-mob.getZ(),source,damage);
    }
    public static void affectMob(ServerPlayer owner,int id,java.util.UUID uuid,int kind,int durationMs,int epoch){
        var entity=owner.level().getEntity(id);
        if(!isBridgeLevel(owner.level())||epoch!=dev.skycraft.world.SkyCollision.epoch()||!(entity instanceof net.minecraft.world.entity.Mob mob)
            ||!mob.getUUID().equals(uuid)||!mob.isAlive()||!isCastleAlly(mob)||dev.skycraft.world.BridgeSimulation.paused(mob)||dev.skycraft.world.BridgeSimulation.cinematic(mob))return;
        int ticks=Math.clamp(durationMs/50,1,1200);
        if(kind==3){mob.igniteForSeconds(ticks/20F);return;}
        if(kind==4&&mob.canFreeze())mob.setTicksFrozen(Math.min(300,mob.getTicksFrozen()+ticks));
        if(kind==5||kind==9||kind==10)CastleMobControl.stun(mob,epoch,ticks);
        var effect=switch(kind){case 1,4,5,7,9,10->net.minecraft.world.effect.MobEffects.SLOWNESS;case 2->net.minecraft.world.effect.MobEffects.POISON;case 6->net.minecraft.world.effect.MobEffects.NAUSEA;case 8->net.minecraft.world.effect.MobEffects.WITHER;default->null;};
        if(effect!=null)mob.addEffect(new net.minecraft.world.effect.MobEffectInstance(effect,ticks,kind==5?4:0));
    }
    

    private static void beforeServerTick(MinecraftServer server) {
        CastleMobControl.tick(server);
        List<ServerPlayer> players = server.getPlayerList().getPlayers();
        ServerPlayer owner = players.stream().filter(dev.skycraft.net.SkyNet::isHost).findFirst().orElse(null);
        if (owner == null || !isBridgeLevel(owner.level())) {
            removeAll();
            restoreTargets();
            bridgeLevel = null;
            return;
        }
        ServerLevel level = owner.level();
        if (bridgeLevel != level) { removeAll(); restoreTargets(); bridgeLevel = level; }
        ++mobScan;
        if (SkyLink.readActors(ACTORS)) sync(level);
        // Discover nearby mobs twice a second, not once per rendered frame or once per player.
        if (mobScan % 10 == 1) {
            for (Entity entity : level.getEntities(owner, owner.getBoundingBox().inflate(64))) {
                if (entity instanceof net.minecraft.world.entity.Mob mob && isCastleAlly(mob)
                    && !BRIDGED_MOBS.containsKey(mob)) {
                    var goals = ((dev.skycraft.mixin.CastleMobAccessor)mob).castlecraft$targets();
                    var previous = new ArrayList<>(goals.getAvailableGoals());
                    goals.removeAllGoals(goal -> true);
                    goals.addGoal(0, new CastleTargetGoal(mob));
                    BRIDGED_MOBS.put(mob, previous);
                    if (mob instanceof net.minecraft.world.entity.NeutralMob angry) angry.stopBeingAngry();
                    SkyCraft.LOG.debug("CastleCraft: {} targets castle enemies while linked", mob.getType());
                }
            }
        }
        for (var it = BRIDGED_MOBS.entrySet().iterator(); it.hasNext();) {
            var entry = it.next();
            var mob = entry.getKey();
            if (mob == null || mob.isRemoved()) { it.remove(); continue; }
            if (mob.level() != level) { restoreTarget(mob, entry.getValue()); it.remove(); continue; }
            if (!(mob.getTarget() instanceof SkyrimActorEntity actor && actor.castleHostile && actor.isAlive())) mob.setTarget(null);
            if (mob.getLastHurtByMob() instanceof net.minecraft.world.entity.player.Player) mob.setLastHurtByMob(null);
            var brain = mob.getBrain();
            if (brain.checkMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET,
                net.minecraft.world.entity.ai.memory.MemoryStatus.REGISTERED)) {
                var target = brain.getMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET).orElse(null);
                if (!(target instanceof SkyrimActorEntity actor && actor.castleHostile && actor.isAlive())) {
                    brain.eraseMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET);
                    if (mobScan % 5 == 0) {
                        SkyrimActorEntity castle = nearestEnemy(mob, 32);
                        if (castle != null) brain.setMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET, castle);
                    }
                }
            }
            if (mob instanceof net.minecraft.world.entity.monster.warden.Warden warden) {
                for (ServerPlayer player : players) warden.clearAnger(player);
                if (mobScan % 10 == 0) {
                    SkyrimActorEntity castle = nearestEnemy(mob, 32);
                    if (castle != null) { warden.increaseAngerAt(castle, 80, false); warden.setAttackTarget(castle); }
                }
            }
        }
    }

    private static @Nullable SkyrimActorEntity nearestEnemy(net.minecraft.world.entity.Mob mob, double range) {
        SkyrimActorEntity result = null;
        double best = range * range;
        for (SkyrimActorEntity actor : PROXIES.values()) {
            if (!actor.castleHostile || !actor.isAlive() || actor.level() != mob.level()) continue;
            // Boss reach is to the actual near body surface, not its far-away transform origin.
            double distance = actor.getBoundingBox().distanceToSqr(mob.getBoundingBox());
            if (distance < best && mob.hasLineOfSight(actor)) { best = distance; result = actor; }
        }
        return result;
    }

    private static void restoreTarget(net.minecraft.world.entity.Mob mob, List<net.minecraft.world.entity.ai.goal.WrappedGoal> old) {
        var selector = ((dev.skycraft.mixin.CastleMobAccessor)mob).castlecraft$targets();
        selector.removeAllGoals(goal -> true);
        for (var wrapped : old) selector.addGoal(wrapped.getPriority(), wrapped.getGoal());
        if (mob.getTarget() instanceof SkyrimActorEntity) mob.setTarget(null);
        var brain = mob.getBrain();
        if (brain.checkMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET,
            net.minecraft.world.entity.ai.memory.MemoryStatus.REGISTERED)
            && brain.getMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET).orElse(null) instanceof SkyrimActorEntity)
            brain.eraseMemory(net.minecraft.world.entity.ai.memory.MemoryModuleType.ATTACK_TARGET);
    }

    private static void restoreTargets() {
        BRIDGED_MOBS.forEach((mob, goals) -> { if (mob != null && !mob.isRemoved()) restoreTarget(mob, goals); });
        BRIDGED_MOBS.clear();
    }

    private static void serverTick(MinecraftServer server) {
        if (bridgeLevel == null || !isBridgeLevel(bridgeLevel)) { OUTBOUND_HITS.clear(); outboundLevel=null; return; }
        if (outboundLevel != bridgeLevel) { OUTBOUND_HITS.clear(); outboundLevel = bridgeLevel; }
        for (ServerPlayer player : bridgeLevel.players()) pickUpNearby(player);
        // Flush before the next actor snapshot can remove a body; one batch for each damage type.
        for (SkyrimActorEntity proxy : PROXIES.values()) {
            if (mobScan % 10 == 0) for (var effect : proxy.getActiveEffects()) {
                String name = effect.getEffect().value().getDescriptionId();
                int kind = switch (name) {
                    case "effect.minecraft.poison", "effect.minecraft.wither" -> 2;
                    case "effect.minecraft.slowness" -> 1;
                    case "effect.minecraft.blindness", "effect.minecraft.darkness", "effect.minecraft.nausea" -> 5;
                    default -> -1;
                };
                if (kind >= 0) SkyLink.pushEvent(6, proxy.formId(), kind, effect.getDuration(), effect.getAmplifier(), 0, 0);
            }
            for (var hit : proxy.takeHits()) {
                OUTBOUND_HITS.addLast(new PendingHit(proxy.formId(),hit));
                ++damageEvents;
                SkyCraft.LOG.debug("CastleCraft hit actor={} damage={} flags={}", proxy.formId(), hit.damage(), hit.flags());
            }
        }
        while (!OUTBOUND_HITS.isEmpty()) {
            var pending = OUTBOUND_HITS.peekFirst(); var hit = pending.hit();
            if (!SkyLink.tryPushEvent(Proto.EV_HIT_ACTOR,pending.actor(),hit.damage(),hit.pushX(),hit.pushZ(),hit.pushStrength(),hit.flags(),hit.weapon())) break;
            OUTBOUND_HITS.removeFirst();
        }
        if (mobScan % 200 == 0) {
            SkyCraft.LOG.info("CastleCraft combat: actors={}, alliedMobs={}, damageEventsLast10s={}", PROXIES.size(), BRIDGED_MOBS.size(), damageEvents);
            damageEvents = 0;
        }
    }

    private static void sync(ServerLevel level) {
        Map<Integer, SkyLink.Actor> live = new HashMap<>();
        for (SkyLink.Actor actor : ACTORS) if (!actor.dead()) live.put(actor.formId(), actor);
        for (Iterator<Map.Entry<Integer, SkyrimActorEntity>> it = PROXIES.entrySet().iterator(); it.hasNext();) {
            var entry = it.next();
            SkyrimActorEntity proxy = entry.getValue();
            if (!live.containsKey(entry.getKey()) || proxy.isRemoved() || proxy.level() != level) {
                proxy.discard(); it.remove();
            }
        }
        int before = PROXIES.size();
        for (SkyLink.Actor actor : live.values()) {
            if (!Float.isFinite(actor.x()) || !Float.isFinite(actor.y()) || !Float.isFinite(actor.z())) continue;
            SkyrimActorEntity proxy = PROXIES.get(actor.formId());
            boolean created = proxy == null;
            if (created) { proxy = new SkyrimActorEntity(SKYRIM_ACTOR, level); proxy.setFormId(actor.formId()); }
            proxy.castleHostile = actor.hostile();
            proxy.castleUndead = (actor.flags() & 128) != 0;
            proxy.castleArthropod = (actor.flags() & 256) != 0;
            
            var maxHealth=proxy.getAttribute(net.minecraft.world.entity.ai.attributes.Attributes.MAX_HEALTH);
            if(maxHealth!=null)maxHealth.setBaseValue(CastleActorVitality.maximum(actor.flags(),actor.level()));
            float nativeHealth=CastleActorVitality.health(actor.healthFrac(),proxy.getMaxHealth());
            if(Float.isFinite(nativeHealth))proxy.setHealth(nativeHealth);
            proxy.castleMovable = (actor.flags() & 64) != 0;
            proxy.setCastleInteractable((actor.flags() & 32) != 0);
            // With rectangular-footprint flag, the unused yaw word carries floor depth in blocks.
            float depth = (actor.flags() & ACTOR_RECTANGULAR_FOOTPRINT) != 0 ? actor.yaw() : actor.width();
            proxy.setSize(actor.width(), actor.height(), depth);
            proxy.setPos(actor.x(), actor.y(), actor.z());
            if (created) {
                if (!actor.name().isEmpty()) proxy.setCustomName(Component.literal(actor.name()));
                if (level.addFreshEntity(proxy)) PROXIES.put(actor.formId(), proxy);
            }
            
        }
        if (PROXIES.size() != before) SkyCraft.LOG.debug("CastleCraft: {} native castle combat bodies", PROXIES.size());
    }

	/**
	 * Items and stuck arrows on Skyrim ground rest on its collision voxels, which on steep or rough
	 * terrain can sit a little off from where the player (on Skyrim's exact triangles) stands.
	 * Touch them over a slightly bigger area than vanilla's so walking over them picks them up.
	 * playerTouch applies all of Minecraft's own rules (pickup delay, owner, inventory space).
	 */
	private static void pickUpNearby(ServerPlayer player) {
		if (!player.isAlive() || player.isSpectator()) {
			return;
		}
		for (Entity entity : player.level().getEntities(player, player.getBoundingBox().inflate(1.25, 1.0, 1.25))) {
			if (!entity.isRemoved() && (entity instanceof net.minecraft.world.entity.item.ItemEntity
				|| entity instanceof net.minecraft.world.entity.projectile.arrow.AbstractArrow)) {
				entity.playerTouch(player);
			}
		}
	}

	private static void removeAll() {
		if (PROXIES.isEmpty()) {
			return;
		}
		PROXIES.values().forEach(Entity::discard);
		PROXIES.clear();
	}

	/**
	 * Skyrim hit the player. Runs on the server thread. {@code kind} is a Proto.HURT_* value and
	 * {@code skyrimDamage} is what Skyrim would have taken off the player's health.
	 */
	public static void hurtPlayer(ServerPlayer player, int kind, float skyrimDamage, int attackerFormId, int flags) {
		if (!isBridgeLevel(player.level()) || !player.isAlive() || !Float.isFinite(skyrimDamage) || skyrimDamage <= 0.0F) {
			return;
		}
		ServerLevel level = player.level();
		SkyrimActorEntity attacker = PROXIES.get(attackerFormId);
		if (attacker != null && attacker.distanceToSqr(player) > 24.0 * 24.0) {
			attacker = null; // a guest's own NPC with the same form id as one of the host's
		}
		DamageSources sources = level.damageSources();
		DamageSource source = switch (kind) {
			case 4 -> sources.onFire();
			case 6 -> sources.freeze();
			case Proto.HURT_MELEE -> attacker != null ? sources.mobAttack(attacker) : sources.generic();
			case Proto.HURT_PROJECTILE -> attacker != null ? sources.mobProjectile(attacker, attacker) : sources.generic();
			case Proto.HURT_MAGIC -> attacker != null ? sources.indirectMagic(attacker, attacker) : sources.magic();
			default -> sources.generic();
		};
        // A snapshot can arrive before its entity is created. Keep source position for vanilla shields.
        if (attacker == null && (kind == Proto.HURT_MELEE || kind == Proto.HURT_PROJECTILE)) {
            for (var actor : ACTORS) if (actor.formId() == attackerFormId) {
                var type = kind == Proto.HURT_MELEE ? net.minecraft.world.damagesource.DamageTypes.MOB_ATTACK
                    : net.minecraft.world.damagesource.DamageTypes.ARROW;
                source = new DamageSource(level.registryAccess().lookupOrThrow(Registries.DAMAGE_TYPE).getOrThrow(type),
                    new net.minecraft.world.phys.Vec3(actor.x(), actor.y(), actor.z()));
                break;
            }
        }
		float damage = skyrimDamage / SKYRIM_TO_MC_DAMAGE;
        
		float healthBefore = player.getHealth();
		boolean blocking = player.isBlocking();
		boolean hurt = player.hurtServer(level, source, damage);
		trainDefence(player, damage, blocking && player.getHealth() >= healthBefore - 1.0E-3F);
		SkyCraft.LOG.info("SkyCraft: Skyrim hit the player for {} ({} Minecraft): health {} -> {}{}", skyrimDamage, damage, healthBefore, player.getHealth(),
			hurt ? "" : " (blocked/immune)");
		if (hurt && attacker != null && (flags & Proto.HURT_POWER_ATTACK) != 0 && !player.isBlocking()) {
			// Power attacks shove harder, like a sprint hit does in Minecraft.
			player.knockback(0.5, attacker.getX() - player.getX(), attacker.getZ() - player.getZ(), source, damage);
		}
	}

	/**
	 * Skyrim skills for taking a hit: Block when the shield caught it, otherwise Light or Heavy
	 * Armor by what the player mostly wears (leather, chainmail, gold, copper and turtle count as
	 * light; iron, diamond and netherite as heavy). Only the host's own Skyrim is told.
	 */
	private static void trainDefence(ServerPlayer player, float damage, boolean blocked) {
		if (!dev.skycraft.net.SkyNet.isHost(player) || damage <= 0.0F) {
			return;
		}
		if (blocked) {
			SkyLink.pushEvent(Proto.EV_SKILL_USE, Proto.SKILL_BLOCK, damage, 0.0F, 0.0F, 0.0F, 0);
			return;
		}
		int light = 0, heavy = 0;
		for (var slot : new net.minecraft.world.entity.EquipmentSlot[] { net.minecraft.world.entity.EquipmentSlot.HEAD, net.minecraft.world.entity.EquipmentSlot.CHEST,
			net.minecraft.world.entity.EquipmentSlot.LEGS, net.minecraft.world.entity.EquipmentSlot.FEET }) {
			var stack = player.getItemBySlot(slot);
			if (stack.isEmpty()) {
				continue;
			}
			String path = net.minecraft.core.registries.BuiltInRegistries.ITEM.getKey(stack.getItem()).getPath();
			if (path.startsWith("iron_") || path.startsWith("diamond_") || path.startsWith("netherite_")) {
				heavy++;
			} else {
				light++;
			}
		}
		if (light + heavy > 0) {
			SkyLink.pushEvent(Proto.EV_SKILL_USE, heavy > light ? Proto.SKILL_HEAVY_ARMOR : Proto.SKILL_LIGHT_ARMOR, damage * (light + heavy) / 4.0F, 0.0F, 0.0F,
				0.0F, 0);
		}
	}

	/** Form id of the Skyrim actor behind a damage source, or 0. */
	public static int attackerFormId(DamageSource source) {
		return source.getEntity() instanceof SkyrimActorEntity proxy ? proxy.formId() : 0;
	}
}


