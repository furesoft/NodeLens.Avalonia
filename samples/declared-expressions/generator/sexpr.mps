<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9000-000000000011" name="sexpr">
  <nodes>
    <node id="d0000000-0000-4000-8000-000000000901" concept="TextGen.Generator" name="Sexpr">
      <properties>
        <name>Sexpr</name>
      </properties>
      <children role="mappings">
        <node id="d0000000-0000-4000-8000-000000000902" concept="TextGen.Mapping" name="Decl.Int">
          <properties>
            <conceptKey>Decl.Int</conceptKey>
          </properties>
          <children role="template">
            <node id="d0000000-0000-4000-8000-000000000903" concept="TextGen.Template">
              <children role="parts">
                <node id="d0000000-0000-4000-8000-000000000904" concept="TextGen.Text" name="(int ">
                  <properties>
                    <text>(int </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000905" concept="TextGen.Property" name="value">
                  <properties>
                    <conceptKey>value</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000906" concept="TextGen.Text" name=")">
                  <properties>
                    <text>)</text>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="d0000000-0000-4000-8000-000000000907" concept="TextGen.Mapping" name="Decl.Bool">
          <properties>
            <conceptKey>Decl.Bool</conceptKey>
          </properties>
          <children role="template">
            <node id="d0000000-0000-4000-8000-000000000908" concept="TextGen.Template">
              <children role="parts">
                <node id="d0000000-0000-4000-8000-000000000909" concept="TextGen.Text" name="(bool ">
                  <properties>
                    <text>(bool </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000090a" concept="TextGen.Property" name="value">
                  <properties>
                    <conceptKey>value</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000090b" concept="TextGen.Text" name=")">
                  <properties>
                    <text>)</text>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="d0000000-0000-4000-8000-00000000090c" concept="TextGen.Mapping" name="Decl.Binary">
          <properties>
            <conceptKey>Decl.Binary</conceptKey>
          </properties>
          <children role="template">
            <node id="d0000000-0000-4000-8000-00000000090d" concept="TextGen.Template">
              <children role="parts">
                <node id="d0000000-0000-4000-8000-00000000090e" concept="TextGen.Text" name="(binary ">
                  <properties>
                    <text>(binary </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000090f" concept="TextGen.Property" name="operator">
                  <properties>
                    <conceptKey>operator</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000910" concept="TextGen.Text" name=" ">
                  <properties>
                    <text> </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000911" concept="TextGen.Child" name="left">
                  <properties>
                    <conceptKey>left</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000912" concept="TextGen.Text" name=" ">
                  <properties>
                    <text> </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000913" concept="TextGen.Child" name="right">
                  <properties>
                    <conceptKey>right</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000914" concept="TextGen.Text" name=")">
                  <properties>
                    <text>)</text>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="d0000000-0000-4000-8000-000000000915" concept="TextGen.Mapping" name="Decl.If">
          <properties>
            <conceptKey>Decl.If</conceptKey>
          </properties>
          <children role="template">
            <node id="d0000000-0000-4000-8000-000000000916" concept="TextGen.Template">
              <children role="parts">
                <node id="d0000000-0000-4000-8000-000000000917" concept="TextGen.Text" name="(if ">
                  <properties>
                    <text>(if </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000918" concept="TextGen.Child" name="condition">
                  <properties>
                    <conceptKey>condition</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-000000000919" concept="TextGen.Text" name=" ">
                  <properties>
                    <text> </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000091a" concept="TextGen.Child" name="then">
                  <properties>
                    <conceptKey>then</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000091b" concept="TextGen.Text" name=" ">
                  <properties>
                    <text> </text>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000091c" concept="TextGen.Child" name="else">
                  <properties>
                    <conceptKey>else</conceptKey>
                  </properties>
                </node>
                <node id="d0000000-0000-4000-8000-00000000091d" concept="TextGen.Text" name=")">
                  <properties>
                    <text>)</text>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>