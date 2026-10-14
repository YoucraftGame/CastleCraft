package dev.skycraft.combat;
import java.lang.reflect.Method;
import java.util.*;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.level.Level;
import net.minecraft.core.Holder;
import net.minecraft.core.Registry;
import net.minecraft.resources.Identifier;
/** Optional Spell Engine 1.10 mob-delivery adapter. Never invokes a Player casting API. */
public final class CastleSpellEngineSkill implements CastleHunterTactics.Skill {
 private final Method container,ids,registry,instant,channeled,range,cooldown,power,focus,contextPower,contextTarget,search,deliver;
 private final Class<?> contextType;
 private final WeakHashMap<SkyrimActorEntity,Map<String,Long>> cooldowns=new WeakHashMap<>();
 public CastleSpellEngineSkill() throws ReflectiveOperationException {
  var helper=Class.forName("net.spell_engine.api.spell.container.SpellContainerHelper");
  container=helper.getMethod("containerFromItemStack",ItemStack.class);
  ids=Class.forName("net.spell_engine.api.spell.container.SpellContainer").getMethod("spell_ids");
  registry=Class.forName("net.spell_engine.api.spell.registry.SpellRegistry").getMethod("from",Level.class);
  var spell=Class.forName("net.spell_engine.api.spell.Spell");var params=Class.forName("net.spell_engine.internals.SpellParameters");
  instant=params.getMethod("isInstant",spell);channeled=params.getMethod("isChanneled",spell);
  range=params.getMethod("getRange",LivingEntity.class,Holder.class);cooldown=params.getMethod("getCooldownDuration",LivingEntity.class,Holder.class);
  var school=Class.forName("net.spell_power.api.SpellSchool");var result=Class.forName("net.spell_power.api.SpellPower$Result");
  power=Class.forName("net.spell_power.api.SpellPower").getMethod("getSpellPower",school,LivingEntity.class);
  var mode=Class.forName("net.spell_engine.internals.target.SpellTarget$FocusMode");
  focus=Class.forName("net.spell_engine.internals.target.SpellIntents").getMethod("focusMode",spell);
  contextType=Class.forName("net.spell_engine.internals.SpellExecution$ImpactContext");
  contextPower=contextType.getMethod("power",result);contextTarget=contextType.getMethod("target",mode);
  var searchType=Class.forName("net.spell_engine.internals.target.SpellTarget$SearchResult");
  search=searchType.getMethod("of",net.minecraft.world.entity.Entity.class);
  deliver=Class.forName("net.spell_engine.internals.delivery.SpellDelivery").getMethod("resolveAndDeliver",Level.class,LivingEntity.class,Holder.class,searchType,contextType,java.util.function.Consumer.class);
 }
 @SuppressWarnings("unchecked") private List<String> spells(ItemStack stack) throws ReflectiveOperationException {
  var value=container.invoke(null,stack);return value==null?List.of():(List<String>)ids.invoke(value);
 }
 @Override public boolean supports(ItemStack stack){try{return !spells(stack).isEmpty();}catch(ReflectiveOperationException ex){return false;}}
 private static Object field(Object object,String name) throws ReflectiveOperationException{return object.getClass().getField(name).get(object);}
 @Override public boolean cast(SkyrimActorEntity actor,LivingEntity target,int state){
  if(!CastleHunterActionClock.attacks(state)||dev.skycraft.world.BridgeSimulation.paused(actor)||dev.skycraft.world.BridgeSimulation.cinematic(actor))return false;
  try{
   var entries=(Registry<?>)registry.invoke(null,actor.level());var timers=cooldowns.computeIfAbsent(actor,a->new HashMap<>());long now=actor.level().getGameTime();
   for(String name:spells(actor.getMainHandItem())){
    var id=Identifier.tryParse(name);if(id==null||timers.getOrDefault(name,0L)>now)continue;
    var holder=entries.get(id).orElse(null);if(holder==null)continue;var spell=holder.value();
    if(!"ACTIVE".equals(String.valueOf(field(spell,"type")))||!(boolean)instant.invoke(null,spell)||(boolean)channeled.invoke(null,spell))continue;
    // Generic player-only delivery/costs need their own adapter. Do not bypass them.
    var delivery=field(spell,"deliver");String kind=String.valueOf(field(delivery,"type"));
    if(!kind.equals("DIRECT")&&!kind.equals("PROJECTILE")&&!kind.equals("METEOR"))continue;
    var cost=field(spell,"cost");if(cost!=null&&(field(cost,"item")!=null||field(cost,"effect_id")!=null&&!String.valueOf(field(cost,"effect_id")).isEmpty()))continue;
    float reach=((Number)range.invoke(null,actor,holder)).floatValue();if(actor.distanceToSqr(target)>reach*reach||!actor.hasLineOfSight(target))continue;
    float cd=((Number)cooldown.invoke(null,actor,holder)).floatValue();
    var context=contextType.getConstructor().newInstance();context=contextPower.invoke(context,power.invoke(null,field(spell,"school"),actor));context=contextTarget.invoke(context,focus.invoke(null,spell));
    actor.setYRot((float)(Math.toDegrees(Math.atan2(target.getZ()-actor.getZ(),target.getX()-actor.getX()))-90));
    java.util.function.Consumer<Object> completion=ignored->{};
    if((boolean)deliver.invoke(null,actor.level(),actor,holder,search.invoke(null,target),context,completion)){
     timers.put(name,now+Math.max(1,(long)Math.ceil(cd*20)));
     if(cost!=null){int wear=((Number)field(cost,"durability")).intValue();if(wear>0)actor.getMainHandItem().hurtAndBreak(wear,actor,net.minecraft.world.entity.EquipmentSlot.MAINHAND);}
     dev.skycraft.SkyCraft.LOG.debug("Hunter independent Spell Engine cast actor={} spell={}",actor.formId(),name);return true;
    }
   }
  }catch(ReflectiveOperationException|RuntimeException ex){dev.skycraft.SkyCraft.LOG.debug("Hunter spell not supported by mob delivery",ex);}
  return false;
 }
 public static void install(){
  if(!net.fabricmc.loader.api.FabricLoader.getInstance().isModLoaded("spell_engine"))return;
  try{CastleHunterTactics.register(new CastleSpellEngineSkill());dev.skycraft.SkyCraft.LOG.info("Hunter Spell Engine mob adapter enabled: instant active direct/projectile/meteor spells without player-only costs");}
  catch(ReflectiveOperationException ex){dev.skycraft.SkyCraft.LOG.warn("Hunter Spell Engine API differs; independent vanilla combat retained",ex);}
 }
}
