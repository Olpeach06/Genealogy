using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Genealogy.Classes
{
    public static class Session
    {
        // Данные пользователя
        public static int UserId { get; set; }
        public static string Username { get; set; }
        public static string Email { get; set; }
        public static int RoleId { get; set; }
        public static string RoleName { get; set; }
        public static int? PersonId { get; set; }
        public static string FullName { get; set; }
        public static DateTime LoginTime { get; set; }

        // Текущее дерево
        public static int CurrentTreeId { get; set; } // ID текущего дерева (по умолчанию 1)

        // Режим гостя
        public static bool IsGuest { get; set; } = false;

        // Проверка прав (вычисляемые свойства)
        public static bool IsAdmin => RoleId == 1;
        public static bool IsEditor => RoleId == 2 || IsAdmin; // Редактор или админ
        public static bool IsViewer => RoleId == 3;

        // Сброс сессии (выход)
        public static void Clear()
        {
            UserId = 0;
            Username = null;
            Email = null;
            RoleId = 0;
            RoleName = null;
            PersonId = null;
            FullName = null;
            LoginTime = DateTime.MinValue;
            CurrentTreeId = 1;
            IsGuest = false;
        }
    }
}