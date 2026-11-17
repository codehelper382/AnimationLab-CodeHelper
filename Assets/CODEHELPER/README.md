# CODEHELPER — Unity equipment framework

Este repositorio contiene **solo CODEHELPER**, no el proyecto Unity completo.

```text
Runtime/
├── Animation/
├── Camera/
├── Core/
├── Equipment/
│   ├── Firearms/       # FirearmDefinition
│   ├── Melee/          # MeleeDefinition: puños y cuchillo
│   └── Equipment*.cs   # Acciones, estado y presentación compartidos
└── Weapons/            # Poses Idle/Aim
EquipmentAssets/
├── Firearms/AK12/
├── Melee/
│   ├── Fists/
│   └── Knife/
└── Shared/Masks/
Editor/
Documentation/
```

Puños y cuchillo pertenecen a la misma categoría **Melee**. `Is Unarmed` distingue los puños;
no crea otra categoría. El runtime no depende de ensamblados KINEMATION.

## Usar esta carpeta

Copiar el contenido en `Assets/CODEHELPER` de un proyecto Unity **6000.3.14f1**, conservando los
`.meta` incluidos. Las rutas de autoría están centralizadas en `Editor/Equipment/EquipmentAssetPaths.cs`.
La configuración actual espera una escena externa `Assets/Scenes/SampleScene.unity` y perfiles
externos `Assets/AK 12.asset` / `Assets/Base.asset`; no se incluyen porque están fuera de CODEHELPER.

Las escenas, Packages, ProjectSettings y otras carpetas del proyecto **no se publican aquí**.
Tampoco se incluyen modelos/animaciones comerciales, copias convertidas ni clips extraídos,
aunque estén localmente dentro de CODEHELPER. Se excluyen sus `.meta` junto con esos archivos.
Para reproducir la demo completa hay que restaurar esos recursos desde una fuente autorizada,
con sus GUID originales. Este repositorio no es una copia completa de la demo ni concede
derechos sobre recursos de terceros. Los archivos excluidos permanecen en el proyecto local.

## Estado

- AK12: Idle/Aim, munición/recámara por instancia y recarga relativa al arma.
- Melee: puños editables y cuchillo con movimiento original coordinado.
- Cámara, columna, IK y presentación propios; contactos de prueba, sin daño real ni impactos.

Guías: [equipos](Documentation/EQUIPMENT_GUIDE.md), [manos](Documentation/EDITABLE_HAND_TARGETS.md),
[cuchillo](Documentation/AUTHORED_KNIFE_GUIDE.md), [plan](Documentation/PLAN_ARMAS_Y_RECARGAS.md).
