# 👆 Control de Fichajes Biométricos (DevsFingerPrint)

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2%20%2F%204.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Windows Forms](https://img.shields.io/badge/Windows%20Forms-WinForms-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![License](https://img.shields.io/badge/Licencia-MIT-green?style=for-the-badge)

**DevsFingerPrint** es una aplicación de escritorio cliente desarrollada en C# y Windows Forms diseñada para la gestión, captura e identificación de huellas dactilares para el control de asistencia y fichadas de empleados en tiempo real. 

El sistema está optimizado para funcionar en segundo plano mediante la bandeja del sistema (System Tray), sincronizar datos localmente y comunicarse con APIs REST externas.

---

## 🌟 Características Principales

* 🔒 **Autenticación e Inicio de Sesión:** Pantalla de Login segura con gestión de credenciales guardadas localmente (`CredentialStorage`).
* 🏢 **Selección de Sucursal y Empresa:** Permite configurar la sucursal activa (`SeleccionSucursalForm`) para el registro correcto de marcaciones.
* ✋ **Enrolamiento de Huellas Dactilares:** Interfaz gráfica interactiva con un selector de dedos (`SelectorDedosControl`) para capturar y enrolar plantillas biométricas por empleado.
* 🎧 **Escucha Biométrica en Segundo Plano:** Módulo `LectorListenerForm` que corre minimizado en la barra de tareas (`MainTrayContext`) procesando fichadas al instante.
* ⚡ **Almacenamiento Local (Offline First):** Integración con **SQLite** (`LocalDatabase`) para garantizar que las fichadas no se pierdan ante cortes de conexión.
* 📡 **Sincronización mediante API REST:** Cliente HTTP (`ApiClient`) encargado de enviar las fichadas y sincronizar catálogos con el servidor central.

---

## 🏗️ Arquitectura del Proyecto

El proyecto sigue una estructura limpia basada en principios de **Clean Architecture / DDD (Domain-Driven Design)** para mantener desacoplada la interfaz de usuario, la lógica de negocio y la infraestructura:
