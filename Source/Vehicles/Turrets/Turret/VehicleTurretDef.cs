﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;
using SmashTools;
using HarmonyLib;

namespace Vehicles
{
	public class VehicleTurretDef : Def, ITweakFields
	{
		/// <summary>
		/// Turret Type
		/// </summary>
		public TurretType turretType = TurretType.Rotatable;

		/// <summary>
		/// Motes spawned after firing VehicleTurret
		/// </summary>
		public List<AnimationProperties> motes;

		/// <summary>
		/// Fields related to ammunition and firing
		/// </summary>
		public ThingFilter ammunition;

		[TweakField(SettingsType = UISettingsType.IntegerBox)]
		public int magazineCapacity = 1;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		public float chargePerAmmoCount = 1;
		public bool genericAmmo = false;
		public TurretCooldownProperties cooldown;

		/// <summary>
		/// Fields related to recoil
		/// </summary>
		[TweakField(SubCategory = "Turret Recoil")]
		public RecoilProperties recoil;
		[TweakField(SubCategory = "Vehicle Recoil")]
		public RecoilProperties vehicleRecoil;

		/// <summary>
		/// All fields related to gizmo or cannon related textures
		/// baseCannonTexPath is for base plate only (static texture below cannon that represents the floor or attaching point of the cannon)
		/// </summary>
		[TweakField]
		public GraphicDataRGB graphicData;
		//UPDATE - Merge to 1 list
		[TweakField(SubCategory = "Layered Graphics")]
		public List<VehicleTurretRenderData> graphics;

		public string gizmoDescription;
		public string gizmoIconTexPath;
		public float gizmoIconScale = 1f;

		public bool matchParentColor = true;

		/// <summary>
		/// Fields relating to targeting and reloading
		/// </summary>
		[TweakField(SubCategory = "Fire Modes")]
		public List<FireMode> fireModes = new List<FireMode>();
		[TweakField(SettingsType = UISettingsType.Checkbox)]
		public bool autoSnapTargeting = false;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		public float rotationSpeed = 1;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		[NumericBoxValues(MinValue = -1, MaxValue = 9999)]
		public float maxRange = -1;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		[NumericBoxValues(MinValue = 0, MaxValue = 9999)]
		public float minRange = 0;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		public float reloadTimer = 5;
        
        public LinearCurve reloadTimerMultiplierPerCrewCount;

        [TweakField(SettingsType = UISettingsType.FloatBox)]
		public float warmUpTimer = 3;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		public float autoRefuelProportion = 2;

		/// <summary>
		/// Sounds
		/// </summary>
		public SoundDef shotSound;
		public SoundDef reloadSound;

		/// <summary>
		/// Fields relating to targeting
		/// </summary>
		public TargetScanFlags targetScanFlags;

		/// <summary>
		/// Fields relating to the projectile
		/// </summary>
		public ThingDef projectile;
		public CustomHitFlags attachProjectileFlag = null;
		public ProjectileHitFlags? hitFlags;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		public float projectileOffset = 0f;
		[TweakField(SettingsType = UISettingsType.FloatBox)]
		public float projectileSpeed = -1;
		public List<float> projectileShifting = new List<float>();

		public Type restrictionType;

		string ITweakFields.Label => nameof(VehicleTurretDef);

		string ITweakFields.Category => string.Empty;//$"{defName} (Def)";

		public void OnFieldChanged()
		{
		}

		public override void ResolveReferences()
		{
			base.ResolveReferences();
			if (ammunition != null)
			{
				ammunition.ResolveReferences();
			}
			ValidateTargetScanFlags();
		}

		public void PostDefDatabase()
		{
		}

		public override void PostLoad()
		{
			base.PostLoad();
			LongEventHandler.ExecuteWhenFinished(delegate ()
			{
				if (graphicData == null)
				{
					return;
				}
				FixInvalidGraphicDataFields(graphicData);
				if (!graphics.NullOrEmpty())
				{
					foreach (VehicleTurretRenderData renderData in graphics)
					{
						FixInvalidGraphicDataFields(renderData.graphicData);
					}
				}
			});
		}

		private void ValidateTargetScanFlags()
		{
			if (targetScanFlags == TargetScanFlags.None)
			{
				targetScanFlags = TargetScanFlags.NeedActiveThreat | TargetScanFlags.NeedAutoTargetable;
				if (projectile?.projectile != null)
				{
					if (!projectile.projectile.flyOverhead)
					{
						targetScanFlags |= TargetScanFlags.NeedLOSToAll;
					}
					else
					{
						targetScanFlags |= TargetScanFlags.NeedNotUnderThickRoof;
					}
					if (projectile.projectile.ai_IsIncendiary)
					{
						targetScanFlags |= TargetScanFlags.NeedNonBurning;
					}
				}
			}
		}

		private static void FixInvalidGraphicDataFields(GraphicDataRGB graphicData)
		{
			if (graphicData == null)
			{
				return;
			}
			if (graphicData.shaderType == null)
			{
				graphicData.shaderType = ShaderTypeDefOf.Cutout;
			}
			else if (!VehicleMod.settings.main.useCustomShaders)
			{
				graphicData.shaderType = graphicData.shaderType.Shader.SupportsRGBMaskTex(ignoreSettings: true) ? ShaderTypeDefOf.CutoutComplex : graphicData.shaderType;
			}
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors())
			{
				yield return error;
			}
			if (motes.NotNullAndAny(m => m.moteDef is null || m.animationType == AnimationWrapperType.Off))
			{
				yield return $"Invalid fields in <field>motes</field>. <field>moteDef</field> cannot be null and <field>animationType</field> cannot be \"Off\"".ConvertRichText();
			}
			if (graphicData == null && gizmoIconTexPath.NullOrEmpty())
			{
				yield return $"Null graphicData and no gizmoIconTexPath, this turret has no way to be rendered in gizmos.";
			}
			if (fireModes.NullOrEmpty() || fireModes.Any(f => !f.IsValid))
			{
				yield return $"Empty or Invalid FireMode in <field>fireModes</field> list. Must include at least 1 entry with non-negative numbers.".ConvertRichText();
			}
			if (ammunition is null && projectile is null)
			{
				yield return $"Must include either <field>ammunition</field> or a default <field>projectile</field>.".ConvertRichText();
			}
			if (ammunition is null)
			{
				if (genericAmmo)
				{
					yield return $"Turret has no <field>ammunition</field> field, but has been flagged as using <field>genericAmmo</field>. This makes no sense.";
				}
				if (chargePerAmmoCount != 1)
				{
					yield return $"Turret has no <field>ammunition</field> field, but has been assigned <field>chargePerAmmoCount</field>. This makes no sense.";
				}
			}
			if (chargePerAmmoCount <= 0)
			{
				yield return $"<field>chargePerAmmoCount</field> must be greater than 1.".ConvertRichText();
			}
			if (ammunition != null)
			{
				if (!Ext_Mods.HasActiveMod(CompatibilityPackageIds.CombatExtended) && !genericAmmo && !ammunition.AllowedThingDefs.Any(c => c.projectile != null || c.projectileWhenLoaded != null))
				{
					yield return "Non-generic ammo must be a <type>ThingDef</type> with projectile properties.".ConvertRichText();
				}
				if (ammunition.AllowedDefCount == 0)
				{
					yield return "<field>ammunition</field> is non-null but no defs are available to use as ammo. Either omit the field entirely or specify valid <type>ThingDefs</type> to use as ammo.".ConvertRichText();
				}
			}
			if (genericAmmo)
			{
				if (projectile is null)
				{
					yield return "Generic ammo must include a default projectile so the turret knows what to shoot.".ConvertRichText();
				}
				if (ammunition != null && ammunition.AllowedDefCount != 1)
				{
					yield return "Generic ammo turrets will only use the first <type>ThingDef</type> in <field>ammunition</field>. Consider removing all other entries but the first.".ConvertRichText();
				}
			}
			if (fireModes.Any(f => f.ticksBetweenShots > f.ticksBetweenBursts))
			{
				yield return "Setting <field>ticksBetweenBursts</field> with a lower tick count than <field>ticksBetweenShots</field> will produce odd shooting behavior. Please set to either the same amount (fully automatic) or greater than.".ConvertRichText();
			}
		}

		public Vector2 ScaleDrawRatio(VehicleDef vehicleDef, Vector2 size)
		{
			Vector2 drawSize = graphicData.drawSize;
			Vector2 scalar = drawSize / vehicleDef.graphicData.drawSize;

			float width = size.x * vehicleDef.uiIconScale * scalar.x;
			float height = size.y * vehicleDef.uiIconScale * scalar.y;
			
			if (width < height)
			{
				height = width * (drawSize.y / drawSize.x);
			}
			else
			{
				width = height * (drawSize.x / drawSize.y);
			}
			return new Vector2(width, height);
		}
	}
}
