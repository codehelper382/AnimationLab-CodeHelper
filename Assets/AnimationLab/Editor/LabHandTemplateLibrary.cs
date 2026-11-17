using System;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabHandTemplateLibrary
    {
        internal static readonly string[] Names = { "Pistola · cambio de cargador", "Metralleta / subfusil · cargador", "Fusil de asalto · cargador", "Francotirador · ciclo de cerrojo", "Francotirador · bala a bala", "Revólver · gestos de carga", "Escopeta · cartucho a cartucho", "Arco · colocar, tensar y soltar" };
        internal static readonly int[] Lengths = { 80, 90, 100, 100, 120, 120, 120, 120 };
        internal static readonly string[][] Guides =
        {
            new[] { "F 0–20 · Preparar; giro suave del arma OPCIONAL.", "F 20–25 · La mano llega al cargador y cierra dedos.", "F 25–40 · Llevar la mano hacia el lateral del personaje.", "F 40–60 · Volver al cargador; frenar antes de insertar.", "F 60–70 · Asentar, abrir dedos y retirar.", "F 70–80 · Volver a la guardia sin golpe seco." },
            new[] { "F 0–20 · Presentar el arma; giro OPCIONAL.", "F 20–25 · Contactar y cerrar el agarre.", "F 25–42 · Recorrido lateral con arco corto.", "F 42–65 · Regresar al alojamiento con precisión.", "F 65–78 · Asentar y soltar; sin sacudir el contacto.", "F 78–90 · Recuperar la guardia." },
            new[] { "F 0–20 · Anticipación; giro del arma OPCIONAL.", "F 20–28 · Contactar con el cargador.", "F 28–50 · Recorrido al lateral, codo relajado.", "F 50–75 · Presentar e insertar el cargador.", "F 75–90 · Asentar y soltar con una pausa breve.", "F 90–100 · Regresar al apoyo." },
            new[] { "F 0–18 · Acercar la mano al cerrojo.", "F 18–26 · Pinzar y levantar/desbloquear.", "F 26–40 · Tirar hacia atrás en una guía recta.", "F 40–55 · Empujar hacia delante sin arco lateral.", "F 55–64 · Bajar/bloquear; pausa corta.", "F 64–100 · Soltar y recuperar el agarre." },
            new[] { "F 0–25 · Preparar la mano y la abertura (arma opcional).", "F 25–35 · Pinza junto al punto de carga.", "F 35–90 · Repetir suministro → inserción por cartucho.", "F 90–105 · Soltar y despejar la recámara.", "F 105–120 · Volver al reposo; cerrojo por separado." },
            new[] { "F 0–20 · Presentar; abrir tambor por separado si existe.", "F 20–30 · Mano en pinza junto al tambor.", "F 30–90 · Alternar suministro y carga por repetición.", "F 90–105 · Retirar los dedos del tambor.", "F 105–120 · Volver al apoyo; cerrar tambor por separado." },
            new[] { "F 0–25 · Preparar acceso al puerto de carga.", "F 25–35 · Pinza del cartucho junto al puerto.", "F 35–90 · Repetir mano al suministro → puerto.", "F 90–105 · Soltar sin atravesar el guardamonte.", "F 105–120 · Recuperar el apoyo; corredera aparte." },
            new[] { "F 0–20 · Acercar la mano a cuerda/culatín.", "F 20–35 · Enganchar con dedos; nock de flecha aparte.", "F 35–50 · Tensar hacia el cuerpo, sin exceder alcance.", "F 50–85 · Anclar y sostener sin temblor artificial.", "F 85–100 · Soltar; seguimiento corto de la mano.", "F 100–120 · Recuperar suavemente; cuerda/palas aparte." }
        };

        // Compact pose-to-pose staging. The wrist settles at contacts rather than adding noise.
        internal static void Gesture(LabHandTemplate template, float t, int repeats, out float travel, out Vector3 euler, out float grip)
        {
            travel = 0; euler = Vector3.zero; grip = 1;
            int length = Lengths[(int)template]; float f = t * length;
            if (template == LabHandTemplate.Bow)
            {
                travel = S(35, 50, f) * (1 - S(100, 120, f)) * (1 + 0.04f * S(85, 92, f) * (1 - S(100, 110, f)));
                grip = S(20, 35, f) * (1 - S(85, 92, f));
                return;
            }
            if (template == LabHandTemplate.SniperBolt)
            {
                travel = S(26, 40, f) * (1 - S(40, 55, f));
                euler.z = -18 * S(18, 26, f) * (1 - S(55, 64, f));
                grip = S(10, 18, f) * (1 - S(64, 75, f));
                return;
            }
            if (template == LabHandTemplate.SingleRound || template == LabHandTemplate.Revolver || template == LabHandTemplate.Shotgun)
            {
                float start = template == LabHandTemplate.Revolver ? 30 : 35;
                grip = S(start - 8, start, f) * (1 - S(90, 105, f));
                if (f >= start && f <= 90)
                {
                    float phase = Mathf.Repeat((f - start) / (90 - start) * Mathf.Clamp(repeats, 1, 8), 1);
                    travel = phase < 0.45f ? S(0, 0.45f, phase) : 1 - S(0.45f, 1, phase);
                    grip = phase < 0.5f ? 1 - S(0.15f, 0.45f, phase) : S(0.5f, 0.75f, phase);
                }
                return;
            }
            float contact = template == LabHandTemplate.Rifle ? 28 : 25;
            float supply = template == LabHandTemplate.Rifle ? 50 : template == LabHandTemplate.SubmachineGun ? 42 : 40;
            float insert = template == LabHandTemplate.Rifle ? 75 : template == LabHandTemplate.SubmachineGun ? 65 : 60;
            travel = S(contact, supply, f) * (1 - S(supply, insert, f));
            euler = new Vector3(0, -8 * travel, 5 * travel);
            float release = template == LabHandTemplate.Rifle ? 90 : template == LabHandTemplate.SubmachineGun ? 78 : 70;
            grip = S(contact - 5, contact, f) * (1 - S(insert + 3, release, f));
        }

        internal static float Influence(LabHandTemplate template, float t)
        {
            float f = t * Lengths[(int)template];
            float arrive = template == LabHandTemplate.SniperBolt ? 18 : template == LabHandTemplate.Rifle ? 28 : template == LabHandTemplate.Bow ? 20 : template == LabHandTemplate.Revolver ? 30 : template == LabHandTemplate.Pistol || template == LabHandTemplate.SubmachineGun ? 25 : 35;
            float leave = template == LabHandTemplate.Bow ? 100 : template == LabHandTemplate.SniperBolt ? 64 : Lengths[(int)template] - 15;
            return S(0, arrive, f) * (1 - S(leave, Lengths[(int)template], f));
        }

        private static float S(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
    }
}
