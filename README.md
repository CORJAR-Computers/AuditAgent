# AuditAgent - Sistema de Auditoria de Software Corporativo

Sistema completo para auditar equipos empresariales: inventario de hardware,
software instalado, parches de seguridad y red. Desarrollado en C# / .NET 8
con seguridad como prioridad.

## Formatos de Salida

El tecnico puede elegir uno o varios formatos al ejecutar la auditoria:

| Formato | Descripcion | Uso ideal |
|---------|-------------|------------|
| **PDF** | Informe profesional con tablas, secciones y paginacion | Enviar por email, archivo, imprimir |
| **HTML** | Informe visual interactivo con estilos CSS | Ver en navegador, compartir en red |
| **JSON** | Datos estructurados completos | Importar en otro sistema, base de datos |
| **CSV** | Tabla de software instalado | Abrir en Excel, Google Sheets |

## Compilacion

```bash
# Restaurar dependencias (incluye Spectre.Console + QuestPDF)
dotnet restore

# Compilar todo
dotnet build

# Ejecutar tests
dotnet test

# Publicizar agente como EXE unico (con soporte PDF)
dotnet publish src/AuditAgent.Agent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Publicizar CLI
dotnet publish src/AuditAgent.CLI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Uso

### Interactivo (recomendado)

```cmd
# Ejecutar como administrador - muestra menu de formato
AuditAgent.Agent.exe

# CLI con menu interactivo
AuditAgent.CLI.exe
```

Se mostrara un menu donde puedes seleccionar con flechas + espacio los formatos que deseas:

```
Seleccione el formato de salida:
  [x] JSON   - Reporte de datos estructurado (para sistemas)
  [x] HTML   - Informe visual interactivo (para navegador)
  [ ] PDF    - Informe profesional para imprimir o enviar por email
  [x] CSV    - Lista de software en tabla (para Excel)
```

### Por linea de comandos

```cmd
# Generar solo PDF
AuditAgent.CLI.exe -f pdf

# Generar HTML + PDF
AuditAgent.CLI.exe -f html,pdf -o C:\auditoria\pc-001

# JSON + CSV, sin actualizaciones del sistema
AuditAgent.CLI.exe -f json,csv --no-updates

# Modo silencioso (solo JSON)
AuditAgent.CLI.exe -q
```

### Opciones del Agent

```cmd
AuditAgent.Agent.exe                        # Menu interactivo
AuditAgent.Agent.exe --format=html,pdf       # Formatos por argumento
AuditAgent.Agent.exe --no-updates            # Sin actualizaciones
AuditAgent.Agent.exe --use-wmi               # WMI completo (lento)
```

## Informacion que recolecta

### Sistema (WMI Win32_ComputerSystem + Win32_BIOS)
- Nombre del equipo, fabricante, modelo, numero de serie
- Asset tag, UUID, dominio, usuario actual

### Hardware (WMI multiples clases)
- **CPU**: Nombre, cores, hilos, velocidad, socket
- **RAM**: Modulos individuales (fabricante, capacidad, tipo DDR, velocidad)
- **Discos**: Modelo, tipo (SSD/HDD), interfaz (SATA/NVMe), particiones con espacio libre
- **GPU**: Nombre, driver, VRAM
- **Bateria**: Estado, capacidad (solo laptops)
- **BIOS/Motherboard**: Version, fabricante, fecha

### Software (Registry + opcional WMI)
- Todos los programas instalados (x64 y x86)
- Nombre, version, fabricante, fecha de instalacion
- Tamano estimado, ruta de instalacion, arquitectura
- Filtro configurable para excluir actualizaciones

### Sistema Operativo (WMI Win32_OperatingSystem)
- Version, build, arquitectura, organizacion
- Fecha de instalacion, ultimo arranque

### Parches de Seguridad (WMI Win32_QuickFixEngineering)
- Lista completa de KBs instalados
- Fecha y usuario que instalo cada parche

### Red (WMI Win32_NetworkAdapterConfiguration)
- Adaptadores activos con IP, MAC, DNS, gateway
- Estado de DHCP, sufijo DNS

## Seguridad

### Cifrado AES-256-GCM
- Cifrado autenticado (confidencialidad + integridad en uno)
- Nonce aleatorio unico por cifrado

### Firma Digital RSA-4096
- Cada reporte se firma con la clave privada del agente
- SHA-256 como algoritmo de hash

### Proteccion de claves
- ACL restrictiva: solo Administrators y SYSTEM acceden a la clave privada
- Claves RSA generadas y almacenadas localmente

### Comunicacion mTLS
- TLS 1.3 obligatorio (fallback a 1.2)
- Autenticacion mutua con certificados X.509
- API Key para proteger endpoints de escritura

## Estructura

```
AuditAgent/
+-- src/
|   +-- AuditAgent.Core/       # Modelos, interfaces, orquestador
|   +-- AuditAgent.Collectors/ # Recolectores WMI + Registry
|   +-- AuditAgent.Security/   # Cifrado, firmas, certificados
|   +-- AuditAgent.Agent/      # Agente principal (menu interactivo)
|   +-- AuditAgent.Api/        # Servidor REST API
|   +-- AuditAgent.CLI/        # CLI ligera con selector de formato
+-- tests/AuditAgent.Tests/    # Tests unitarios
+-- reports/                   # Reportes generados (JSON/HTML/PDF/CSV)
```

## Licencia

Proyecto privado. Uso corporativo interno. CORJAR Computers.
