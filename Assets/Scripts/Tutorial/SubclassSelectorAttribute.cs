using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Применяется к [SerializeReference] полю чтобы Inspector показывал
    /// дропдаун выбора подкласса вместо стандартного пустого UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SubclassSelectorAttribute : PropertyAttribute { }
}
