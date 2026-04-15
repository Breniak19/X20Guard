# X20Guard 🛡️

**X20Guard** es una utilidad ligera de sistema desarrollada en C# diseñada para mantener configuraciones personalizadas de pantalla (overclock de hercios) en equipos donde el controlador de gráficos tiende a restablecerse a los valores predeterminados tras eventos de energía.

Esta herramienta fue creada específicamente para solucionar el problema de "apretón de manos" (*handshake*) entre los drivers **Intel HD Graphics** y monitores con overclock (ej. de 60Hz a 100Hz) durante el despertar de la suspensión o hibernación.

## 🚀 Características

* **Monitoreo en Tiempo Real:** Rastrea la resolución activa y la frecuencia de actualización mediante la API `User32.dll`.
* **Recuperación Automática:** Dispara un reinicio de la pila de video (`Win+Ctrl+Shift+B`) si detecta una caída en los hercios configurados (ej. de 100Hz a 60Hz).
* **Feedback Visual:** Icono dinámico en la bandeja del sistema (System Tray) con código de colores:
    * 🟢 **Verde:** Configuración correcta (1440x900 @ 100Hz).
    * 🔴 **Rojo:** Alerta detectada (Hercios por debajo del objetivo).
    * ⚪ **Gris:** Modo espera o resolución distinta (ej. pantalla de laptop activa).
* **Eficiencia Térmica y de Recursos:** Proceso de alta prioridad con consumo de CPU/RAM casi nulo.
* **Caja Negra (Logging):** Sistema automático de registro de errores en el escritorio para facilitar el debug.

## 🛠️ Requisitos

* **Sistema Operativo:** Windows 10 / 11.
* **Hardware:** Monitor con overclock configurado previamente (ej. mediante CRU).
* **Framework:** .NET Runtime (compatible con Visual Studio 2022).

## 📦 Instalación y Configuración

1.  **Compilación:** Compila el proyecto en Visual Studio 2022 en modo *Release*.
2.  **Privilegios:** Asegúrate de ejecutar la aplicación como **Administrador** para permitir la simulación de teclas globales.
3.  **Persistencia:** Se recomienda configurar una tarea en el **Programador de Tareas de Windows**:
    * **Desencadenador:** Al iniciar sesión.
    * **Configuración:** Ejecutar con los privilegios más altos.
    * **Condiciones:** Desmarcar "Detener si el equipo pasa a batería".

## 💻 Uso

Una vez iniciada, la aplicación se ocultará automáticamente en el área de iconos ocultos. 
* **Clic derecho:** Permite forzar un reinicio manual del driver de video o salir de la aplicación.
* **Hover (Pasar el ratón):** Muestra un *tooltip* con la resolución y frecuencia detectada en tiempo real.

## ⚠️ Notas de Desarrollo

La aplicación utiliza un retraso inteligente de 4 segundos tras el despertar del sistema (`PowerModes.Resume`). Esto permite que el hardware de video se estabilice antes de realizar la verificación, evitando parpadeos innecesarios.

---
Desarrollado con ❤️ para la comunidad de entusiastas del hardware.
