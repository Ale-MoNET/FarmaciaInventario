#!/usr/bin/env python3
"""
Script de prueba del lector de barras
Simula escaneos y verifica la lógica de búsqueda
"""

def test_barcode_search():
    # Medicamentos de prueba (simular BD)
    medicamentos = [
        {"id": 1, "codigo": "7501001234567", "nombre": "Paracetamol 500mg", "existencias": 100},
        {"id": 2, "codigo": "7501001234568", "nombre": "Ibuprofeno 400mg", "existencias": 50},
        {"id": 3, "codigo": "ASPIRIN001", "nombre": "Aspirina 100mg", "existencias": 200},
        {"id": 4, "codigo": "AMOXICILINA001", "nombre": "Amoxicilina 500mg", "existencias": 75},
        {"id": 5, "codigo": "VITAMINA_C_1000", "nombre": "Vitamina C 1000mg", "existencias": 150},
    ]

    def buscar_por_termino(termino):
        """Simula la lógica de búsqueda del BarcodeService"""
        if not termino or not termino.strip():
            return medicamentos

        termino_upper = termino.strip().upper()
        resultados = [m for m in medicamentos
                     if termino_upper in m["codigo"].upper()
                     or termino_upper in m["nombre"].upper()]
        return resultados

    # Test cases
    test_cases = [
        ("7501001234567", 1, "Búsqueda exacta de código"),
        ("750100", 2, "Búsqueda parcial de código"),
        ("Paracetamol", 1, "Búsqueda por nombre"),
        ("mag", 1, "Búsqueda parcial en nombre (Paracetamol)"),
        ("ASPIR", 1, "Búsqueda insensible a mayúsculas"),
        ("Vitamina", 1, "Búsqueda de vitamina"),
        ("C", 1, "Búsqueda simple por letra"),
        ("", 5, "Sin filtro (todos)"),
        ("INEXISTENTE", 0, "Código no existe"),
    ]

    print("=" * 70)
    print("PRUEBA DE LECTOR DE BARRAS - SISTEMA FARMAGESTION")
    print("=" * 70)
    print(f"\n📦 Medicamentos en base de datos: {len(medicamentos)}\n")

    for codigo, nombre, _, existencias in [(m["codigo"], m["nombre"], m["id"], m["existencias"])
                                           for m in medicamentos]:
        print(f"  • [{codigo}] {nombre} ({existencias} unidades)")

    print("\n" + "=" * 70)
    print("EJECUTANDO PRUEBAS DE BÚSQUEDA")
    print("=" * 70 + "\n")

    passed = 0
    failed = 0

    for termino, expected_count, descripcion in test_cases:
        resultados = buscar_por_termino(termino)
        actual_count = len(resultados)

        passed_test = actual_count == expected_count
        status = "✅ PASS" if passed_test else "❌ FAIL"

        if passed_test:
            passed += 1
        else:
            failed += 1

        print(f"{status} | {descripcion}")
        print(f"       Búsqueda: '{termino}' | Esperado: {expected_count} | Obtenido: {actual_count}")

        if resultados:
            for med in resultados:
                print(f"         → [{med['codigo']}] {med['nombre']}")
        else:
            print(f"         → (sin resultados)")
        print()

    print("=" * 70)
    print(f"RESULTADOS: {passed} pasadas ✅ | {failed} fallidas ❌")
    print("=" * 70)

    return failed == 0

def test_manual():
    """Modo interactivo para probar manualmente"""
    medicamentos = [
        {"codigo": "7501001234567", "nombre": "Paracetamol 500mg"},
        {"codigo": "7501001234568", "nombre": "Ibuprofeno 400mg"},
        {"codigo": "ASPIRIN001", "nombre": "Aspirina 100mg"},
        {"codigo": "AMOXICILINA001", "nombre": "Amoxicilina 500mg"},
        {"codigo": "VITAMINA_C_1000", "nombre": "Vitamina C 1000mg"},
    ]

    print("\n" + "=" * 70)
    print("MODO INTERACTIVO - Simula tu lector de barras")
    print("=" * 70)
    print("Escribe códigos de barras o nombres para probar la búsqueda.")
    print("Escribe 'salir' para terminar.\n")

    while True:
        try:
            busqueda = input("🔍 Ingresa búsqueda (código o nombre): ").strip()

            if busqueda.lower() == "salir":
                print("👋 ¡Hasta luego!")
                break

            if not busqueda:
                print("⚠️ Por favor ingresa algo para buscar.\n")
                continue

            # Simular búsqueda
            termino_upper = busqueda.upper()
            resultados = [m for m in medicamentos
                         if termino_upper in m["codigo"].upper()
                         or termino_upper in m["nombre"].upper()]

            print(f"\n📊 Resultados: {len(resultados)} medicamento(s) encontrado(s)\n")

            if resultados:
                for med in resultados:
                    print(f"  ✓ [{med['codigo']}] {med['nombre']}")
            else:
                print(f"  ✗ No se encontraron medicamentos")

            print()

        except KeyboardInterrupt:
            print("\n\n👋 Programa interrumpido.")
            break
        except Exception as e:
            print(f"❌ Error: {e}\n")

if __name__ == "__main__":
    import sys

    # Ejecutar pruebas automáticas
    success = test_barcode_search()

    # Preguntar si desea modo interactivo
    print("\n¿Deseas probar el modo interactivo? (s/n): ", end="")
    try:
        respuesta = input().strip().lower()
        if respuesta == 's' or respuesta == 'sí' or respuesta == 'si':
            test_manual()
    except:
        pass

    sys.exit(0 if success else 1)
