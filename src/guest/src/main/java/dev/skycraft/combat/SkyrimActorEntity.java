package dev.skycraft.combat;

import dev.skycraft.link.Proto;
import net.minecraft.network.syncher.EntityDataAccessor;
import net.minecraft.network.syncher.EntityDataSerializers;
import net.minecraft.network.syncher.SynchedEntityData;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.sounds.SoundEvent;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntityDimensions;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.HumanoidArm;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.Pose;
import net.minecraft.tags.ItemTags;
import net.minecraft.world.entity.projectile.Projectile;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.level.Level;
import org.jspecify.annotations.Nullable;

/**
 * An invisible stand-in for one Skyrim actor, so Minecraft's own combat (swords, crits, sweeps,
 * enchantments, attack cooldown, bows, tridents) can target and hit Skyrim NPCs. What it receives is
 * collected into one hit per tick and forwarded to the real actor; its own health never drops.
 */
public class SkyrimActorEntity extends LivingEntity {
 public boolean castleHostile;
 public boolean castleMovable;
 public boolean castleUndead,castleArthropod;
 private boolean poisonTick;
 /** Called only from vanilla PoisonMobEffect, so instant harm stays lethal even during poison. */
 public boolean hurtFromCastlePoison(ServerLevel level,DamageSource source,float damage){
  boolean previous=poisonTick;poisonTick=true;
  try{return hurtServer(level,source,damage);}finally{poisonTick=previous;}
 }
 private static final EntityDataAccessor<Boolean> INTERACTIVE = SynchedEntityData.defineId(SkyrimActorEntity.class, EntityDataSerializers.BOOLEAN);
 public void setCastleInteractable(boolean value){entityData.set(INTERACTIVE,value);}
 @Override
 public net.minecraft.world.InteractionResult interact(net.minecraft.world.entity.player.Player player,net.minecraft.world.InteractionHand hand,net.minecraft.world.phys.Vec3 location) {
  if(!entityData.get(INTERACTIVE)||hand!=net.minecraft.world.InteractionHand.MAIN_HAND)return super.interact(player,hand,location);
  if(!level().isClientSide())dev.skycraft.link.SkyLink.pushEvent(8,formId(),0,0,0,0,0);
  return net.minecraft.world.InteractionResult.SUCCESS;
 }
	private static final EntityDataAccessor<Integer> FORM_ID = SynchedEntityData.defineId(SkyrimActorEntity.class, EntityDataSerializers.INT);
	private static final EntityDataAccessor<Float> WIDTH = SynchedEntityData.defineId(SkyrimActorEntity.class, EntityDataSerializers.FLOAT);
	private static final EntityDataAccessor<Float> DEPTH = SynchedEntityData.defineId(SkyrimActorEntity.class, EntityDataSerializers.FLOAT);
	private static final EntityDataAccessor<Float> HEIGHT = SynchedEntityData.defineId(SkyrimActorEntity.class, EntityDataSerializers.FLOAT);

	// This tick's hit, flushed to Skyrim by SkyCombat after all attacks for the tick have landed
	// (Player.attack adds its sprint/enchantment knockback after hurtServer returns).
	private final CastleHitAccumulator hits = new CastleHitAccumulator();
    private double pushX, pushZ;
    private float pushStrength;
    private boolean critical;
    private final net.minecraft.world.entity.InsideBlockEffectApplier.StepBasedCollector blockEffects =
        new net.minecraft.world.entity.InsideBlockEffectApplier.StepBasedCollector();
    private int lastFluidMask = -1;
    private int fluidReportTicks;

    static final int HIT_POISON = 1 << 8, HIT_WITHER = 1 << 9, HIT_FREEZE = 1 << 10;

	public SkyrimActorEntity(EntityType<? extends SkyrimActorEntity> type, Level level) {
		super(type, level);
		this.setNoGravity(true);
		this.noPhysics = false;
		this.setInvisible(true);
		this.setSilent(true);
	}

	public int formId() {
		return this.entityData.get(FORM_ID);
	}

	public void setFormId(int formId) {
		this.entityData.set(FORM_ID, formId);
	}

	@Override
	protected void defineSynchedData(SynchedEntityData.Builder builder) {
		super.defineSynchedData(builder);
		builder.define(FORM_ID, 0);
        builder.define(INTERACTIVE, false);
		builder.define(WIDTH, 0.6F);
		builder.define(HEIGHT, 1.8F);
        builder.define(DEPTH, .6F);
	}

    public void setSize(float width, float height, float depth) {
        width = CastleContactGeometry.dimension(width, .6f);
        height = CastleContactGeometry.dimension(height, 1.8f);
        depth = CastleContactGeometry.dimension(depth, width);
        if (Math.abs(this.entityData.get(WIDTH) - width) > .01F
            || Math.abs(this.entityData.get(HEIGHT) - height) > .01F
            || Math.abs(this.entityData.get(DEPTH) - depth) > .01F) {
            this.entityData.set(WIDTH, width);
            this.entityData.set(HEIGHT, height);
            this.entityData.set(DEPTH, depth);
            this.refreshDimensions();
        }
    }

    @Override
    protected net.minecraft.world.phys.AABB makeBoundingBox(net.minecraft.world.phys.Vec3 pos) {
        // A castle boss is often wide and shallow. Vanilla's square width box misplaced its
        // footprint and made melee navigation stop long before the actual body.
        if (this.entityData == null) return super.makeBoundingBox(pos);
        double halfWidth = this.entityData.get(WIDTH) * .5;
        double halfDepth = this.entityData.get(DEPTH) * .5;
        return new net.minecraft.world.phys.AABB(pos.x-halfWidth, pos.y, pos.z-halfDepth,
            pos.x+halfWidth, pos.y+this.entityData.get(HEIGHT), pos.z+halfDepth);
    }

    @Override
    public boolean fudgePositionAfterSizeChange(EntityDimensions previous) {
        // Native castle movement owns the body. A size update must never search a new MC position.
        return false;
    }

	@Override
	public void onSyncedDataUpdated(EntityDataAccessor<?> accessor) {
		super.onSyncedDataUpdated(accessor);
		if (WIDTH.equals(accessor) || HEIGHT.equals(accessor) || DEPTH.equals(accessor)) {
			this.refreshDimensions();
		}
	}

	@Override
	protected EntityDimensions getDefaultDimensions(Pose pose) {
		return EntityDimensions.scalable(this.entityData.get(WIDTH), this.entityData.get(HEIGHT));
	}

	@Override
	protected void actuallyHurt(ServerLevel level, DamageSource source, float dmg) {
		// Minecraft has applied everything (crit, sharpness, strength, cooldown, invulnerability
		// frames). Hand the result to Skyrim instead of lowering our own health.
		if (this.isInvulnerableTo(level, source) || dmg <= 0.0F) {
			return;
		}
        // Preserve vanilla resistance/protection/absorption; do not run enchantments twice.
        dmg = getDamageAfterMagicAbsorb(source, getDamageAfterArmorAbsorb(source, dmg));
        float absorbed = Math.min(getAbsorptionAmount(), dmg);
        setAbsorptionAmount(getAbsorptionAmount() - absorbed);
        dmg -= absorbed;
        if (!Float.isFinite(dmg) || dmg <= 0) return;
        int flags = 0;
        if (source.getDirectEntity() instanceof Projectile) flags |= Proto.HIT_PROJECTILE;
        if (source.is(net.minecraft.tags.DamageTypeTags.IS_FIRE)) flags |= Proto.HIT_FIRE;
        if (source.is(net.minecraft.world.damagesource.DamageTypes.WITHER)) flags |= HIT_WITHER;
        if (source.is(net.minecraft.world.damagesource.DamageTypes.FREEZE)) flags |= HIT_FREEZE;
        // Exact vanilla poison-call provenance distinguishes MAGIC instant damage from poison.
        if (poisonTick) flags |= HIT_POISON;
        hits.add(dmg, flags, weaponClass(source));
		this.getCombatTracker().recordDamage(source, dmg);
        // A real health delta lets vanilla hit hearts/statistics observe the hit.
        // Native death/drop remains authoritative; the next snapshot restores actual native HP.
        setHealth(Math.max(.001F, getHealth() - dmg));
        gameEvent(net.minecraft.world.level.gameevent.GameEvent.ENTITY_DAMAGE);
	}

	@Override
	public void knockback(double power, double xd, double zd, DamageSource source, float damage, boolean comesFromEffect) {
		// Skyrim owns this actor's position. Remember the strongest push for Skyrim's stagger:
		// Minecraft pushes towards -(xd, zd).
		double len = Math.sqrt(xd * xd + zd * zd);
		if (len > 1e-6 && power > this.pushStrength) {
			this.pushStrength = (float) power;
			this.pushX = -xd / len;
			this.pushZ = -zd / len;
		}
	}

	/** Player.crit() was called on us this tick. */
	public void markCritical() {
		this.critical = true;
	}

	/** Which kind of Skyrim weapon impact this hit should look and sound like. */
	private static int weaponClass(DamageSource source) {
		if (source.getDirectEntity() instanceof net.minecraft.world.entity.projectile.arrow.ThrownTrident) {
			return Proto.WEAPON_PIERCE;
		}
		if (source.getDirectEntity() instanceof Projectile) {
			return Proto.WEAPON_ARROW;
		}
		ItemStack weapon = source.getWeaponItem();
		if (weapon == null && source.getEntity() instanceof LivingEntity attacker) {
			weapon = attacker.getMainHandItem();
		}
		if (weapon == null || weapon.isEmpty()) {
			return Proto.WEAPON_UNARMED;
		}
		if (weapon.is(ItemTags.SWORDS)) {
			return Proto.WEAPON_BLADE;
		}
		if (weapon.is(ItemTags.AXES)) {
			return Proto.WEAPON_AXE;
		}
		if (weapon.is(Items.TRIDENT)) {
			return Proto.WEAPON_PIERCE;
		}
		return Proto.WEAPON_BLUNT;
	}

    /** Flush source categories separately after vanilla combat has applied its attack cooldown. */
    public java.util.List<CastleHitAccumulator.Hit> takeHits() {
        var result = hits.drain((float)pushX, (float)pushZ, pushStrength, critical);
        pushX = pushZ = 0;
        pushStrength = 0;
        critical = false;
        return result;
    }

    /** Run real block contact even though this body is positioned by the castle engine. */
    public void applyCastleBlockContacts(ServerLevel level) {
        var box = getBoundingBox().deflate(1e-5);
        var from = net.minecraft.core.BlockPos.containing(box.minX, box.minY, box.minZ);
        var to = net.minecraft.core.BlockPos.containing(box.maxX, box.maxY, box.maxZ);
        boolean lava = false, water = false;
        blockEffects.advanceStep(0);
        for (var pos : net.minecraft.core.BlockPos.betweenClosed(from, to)) {
            var state = level.getBlockState(pos);
            if (state.isAir()) continue;
            // NOOP used to discard cobweb, fire, extinguishing and other deferred effects.
            state.entityInside(level, pos, this, blockEffects, true);
            var fluid = state.getFluidState();
            if (!fluid.isEmpty() && CastleContactGeometry.touchesFluid(box.minY, box.maxY,
                pos.getY(), fluid.getHeight(level, pos))) {
                if (fluid.is(net.minecraft.tags.FluidTags.LAVA)) lava = true;
                if (fluid.is(net.minecraft.tags.FluidTags.WATER)) water = true;
            }
        }
        blockEffects.applyAndClear(this);
        if (lava) { lavaIgnite(); lavaHurt(); }
        else if (water) extinguishFire();
        int mask = (lava ? 1 : 0) | (water ? 2 : 0);
        if (mask != lastFluidMask || (mask != 0 && ++fluidReportTicks >= 5)) {
            dev.skycraft.link.SkyLink.pushEvent(7, formId(), lava ? 1 : 0, water ? 1 : 0,
                lava ? .5f : water ? .8f : 1, 0, 0);
            if (mask != lastFluidMask) dev.skycraft.SkyCraft.LOG.info(
                "CastleCraft contact actor={} lava={} water={} bounds={}", formId(), lava, water, box);
            lastFluidMask = mask;
            fluidReportTicks = 0;
        }

    }

	@Override
	public void tick() {
		// Position and rotation come from Skyrim (SkyCombat); keep hurt timers and fire ticking.
		this.baseTick();
        if (level() instanceof ServerLevel server && SkyCombat.isBridgeLevel(server)) {
            applyCastleBlockContacts(server);
            if(castleMovable){
                var requested=CastleDisplacement.limit(getDeltaMovement());
                var colliders=Entity.collectAllColliders(this,server,getBoundingBox().expandTowards(requested));
                var move=CastleDisplacement.solve(getBoundingBox(),requested,colliders);
                if(move.lengthSqr()>1e-10)
                    dev.skycraft.link.SkyLink.tryPushEvent(9,formId(),(float)move.x,(float)move.z,(float)getX(),(float)getZ(),dev.skycraft.world.SkyCollision.epoch(),0);
            }
        }
        // Host applies the constrained step. The next snapshot supplies the authoritative position.
        setDeltaMovement(net.minecraft.world.phys.Vec3.ZERO);
	}

	@Override
	public boolean isPushable() {
		return true;
	}

	@Override
	protected void doPush(Entity entity) {
		// Keep the castle actor authoritative, but push approaching Minecraft
		// mobs out of its footprint instead of permitting full overlap.
		if(!(entity instanceof SkyrimActorEntity))entity.push(this);
	}

	@Override
	public boolean canBeCollidedWith(@Nullable Entity other) {
		return other!=null&&!(other instanceof SkyrimActorEntity);
	}

	@Override
	public boolean shouldShowName() {
		return false;
	}

	@Override
	public boolean shouldBeSaved() {
		return false;
	}

	@Override
	protected @Nullable SoundEvent getHurtSound(DamageSource source) {
		return null; // Skyrim plays the NPC's own pain sounds
	}

	@Override
	protected @Nullable SoundEvent getDeathSound() {
		return null;
	}

	@Override
	public HumanoidArm getMainArm() {
		return HumanoidArm.RIGHT;
	}
}
