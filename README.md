# AuditAgent - Sistema de Auditoria de Software Corporativo

Sistema completo para auditar equipos empresariales: inventario de hardware,
software instalado, parches de seguridad y red. Desarrollado en C# / .NET 8
con seguridad como prioridad.

## Interfaz Grafica (Recomendado)

AuditAgent incluye una aplicacion de escritorio WPF con interfaz grafica moderna.
El tecnico puede ejecutar la auditoria con un solo clic y seleccionar los formatos
de salida deseados (HTML, PDF, JSON, CSV).

### Compilar y ejecutar la GUI

```bash
# Restaurar dependencias
dotnet restore

# Compilar la solucion completa
dotnet build

# Ejecutar la GUI (requiere Windows + administrador)
dotnet run --project src/AuditAgent.GUI
```

### Generar aplicacion portable (Single File EXE)

```powershell
# Opcion 1: Script PowerShell
.
\publish-portable.ps1

# Opcion 2: Script BAT (sin PowerShell)
publish-portable.bat

# Opcion 3: Comando directo
dotnet publish src/AuditAgent.GUI -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial `
    -o publish/AuditAgent-Portable
```

El resultado es un **archivo unico `AuditAgent.exe`** que:
- No requiere instalacion de .NET en el PC de destino
- Se puede copiar a un USB y ejecutar en cualquier Windows 10/11 x64
- Incluye todo el runtime de .NET 8 embebido
- Requiere ejecutarse como Administrador (UAC)

### Caracteristicas de la GUI

- Informacion del sistema visible al abrir la app
- Barra de progreso con indicadores por paso (Sistema, Hardware, Software, S.O., Red, Firma)
- Seleccion de multiples formatos de salida (checkboxes)
- Tabla interactiva de software con busqueda en tiempo real
- Resumen visual con tarjetas de estadisticas (software, parches, discos, red)
- Boton para abrir la carpeta de informes generados
- Firma digital RSA-4096 automatica

## Formatos de Salida

| Formato | Descripcion | Uso ideal |
|---------|-------------|------------|
| **HTML** | Informe visual interactivo con estilos CSS | Ver en navegador, compartir en red |
| **PDF** | Informe profesional con tablas, secciones y paginacion | Enviar por email, archivo, imprimir |
| **JSON** | Datos estructurados completos | Importar en otro sistema, base de datos |
| **CSV** | Tabla de software instalado | Abrir en Excel, Google Sheets |

## Compilacion

```bash
# Restaurar dependencias
dotnet restore

# Compilar todo
dotnet build

# Ejecutar tests
dotnet test

# Publicizar GUI como EXE portable
dotnet publish src/AuditAgent.GUI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Publicizar Agent (CLI interactiva)
dotnet publish src/AuditAgent.Agent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Publicizar CLI
dotnet publish src/AuditAgent.CLI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Uso

### GUI (recomendado)

```
1. Copiar AuditAgent.exe al PC a auditar (desde USB o red)
2. Clic derecho > Ejecutar como Administrador
3. Esperar a que cargue la informacion del sistema
4. Presionar "Iniciar Auditoria"
5. Seleccionar formatos deseados (HTML, PDF, JSON, CSV)
6. Presionar "Generar Informe(s)"
7. Los informes se guardan en: Documentos\AuditAgent\Reports\[NombrePC]\[fecha]\
```

### CLI (alternativa)

```cmd
# Menu interactivo con selector de formato
AuditAgent.CLI.exe

# Generar solo PDF
AuditAgent.CLI.exe -f pdf

# Generar HTML + PDF
AuditAgent.CLI.exe -f html,pdf -o C:\auditoria\pc-001

# JSON + CSV, sin actualizaciones del sistema
AuditAgent.CLI.exe -f json,csv --no-updates

# Modo silencioso
AuditAgent.CLI.exe -q
```

### Agent (interactivo)

```cmd
AuditAgent.Agent.exe                        # Menu interactivo
AuditAgent.Agent.exe --format=html,pdf       # Formatos por argumento
AuditAgent.Agent.exe --no-updates            # Sin actualizaciones
```

## Informacion que recolecta

### Sistema (WMI Win32_ComputerSystem + Win32_BIOS)
- Nombre del equipo, fabricante, modelo, numero de serie
- Asset tag, UUID, dominio, usuario actual

### Hardware (WMI multiples clases)
- **CPU**: Nombre, cores, hilos, velocidad, socket
- **RAM**: Modulos individuales (fabricante, capacidad, tipo DDR, velocidad)
- **Discos**: Modelo, tipo (SSD/HDD), interfaz (SATA/NVMe), particiones
- **GPU**: Nombre, driver, VRAM
- **Bateria**: Estado, capacidad (solo laptops)
- **BIOS/Motherboard**: Version, fabricante, fecha

### Software (Registry + opcional WMI)
- Todos los programas instalados (x64 y x86)
- Nombre, version, fabricante, fecha de instalacion
- Tamano estimado, ruta, arquitectura
- Filtro configurable para excluir actualizaciones

### Sistema Operativo (WMI Win32_OperatingSystem)
- Version, build, arquitectura, organizacion, fecha de instalacion

### Parches de Seguridad (WMI Win32_QuickFixEngineering)
- Lista completa de KBs instalados con fecha y usuario

### Red (WMI Win32_NetworkAdapterConfiguration)
- Adaptadores activos con IP, MAC, DNS, gateway, estado DHCP

## Seguridad

### Cifrado AES-256-GCM
- Cifrado autenticado (confidencialidad + integridad)
- Nonce aleatorio unico por cifrado

### Firma Digital RSA-4096
- Cada reporte se firma con la clave privada del agente
- SHA-256 como algoritmo de hash

### Proteccion de claves
- ACL restrictiva: solo Administrators y SYSTEM acceden a la clave privada
- Claves RSA generadas y almacenadas localmente

### Comunicacion mTLS (servidor opcional)
- TLS 1.3 obligatorio (fallback a 1.2)
- Autenticacion mutua con certificados X.509
- API Key con comparacion de tiempo constante para proteger endpoints

## Estructura del Proyecto

```
AuditAgent.sln
+-- src/
|   +-- AuditAgent.Core/       # Modelos, interfaces, orquestador
|   +-- AuditAgent.Collectors/ # Recolectores WMI + Registry
|   +-- AuditAgent.Security/   # Cifrado, firmas, certificados
|   +-- AuditAgent.GUI/        # Aplicacion de escritorio WPF (PRINCIPAL)
|   +-- AuditAgent.Agent/      # Agente consola (menu interactivo)
|   +-- AuditAgent.CLI/        # CLI ligera con selector de formato
|   +-- AuditAgent.Api/        # Servidor REST API (opcional)
+-- tests/AuditAgent.Tests/    # Tests unitarios (xUnit)
+-- publish-portable.ps1       # Script de publicacion portable (PowerShell)
+-- publish-portable.bat       # Script de publicacion portable (BAT)
```

## Licencia

Proyecto privado. Uso corporativo interno. CORJAR Computers.
