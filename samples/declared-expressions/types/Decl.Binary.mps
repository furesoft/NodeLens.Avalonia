<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9002-000000000004" name="Decl.Binary">
  <nodes>
    <node id="d0000000-0000-4000-8002-000000000004" concept="TypeRule.Rule" name="Decl.Binary">
      <properties>
        <conceptKey>Decl.Binary</conceptKey>
      </properties>
      <children role="body">
        <node id="d0000000-0000-4000-8002-000000000104" concept="TypeRule.Switch" name="operator">
          <properties>
            <property>operator</property>
          </properties>
          <children role="cases">
            <node id="d0000000-0000-4000-8002-000000000204" concept="TypeRule.Case" name="==">
              <properties>
                <value>==</value>
              </properties>
              <children role="body">
                <node id="d0000000-0000-4000-8002-000000000304" concept="TypeRule.Fixed">
                  <children role="type">
                    <node id="d0000000-0000-4000-8002-000000000404" concept="TypeRule.Primitive" name="bool">
                      <properties>
                        <name>bool</name>
                      </properties>
                    </node>
                  </children>
                </node>
              </children>
            </node>
            <node id="d0000000-0000-4000-8002-000000000205" concept="TypeRule.Case" name="&lt;">
              <properties>
                <value>&lt;</value>
              </properties>
              <children role="body">
                <node id="d0000000-0000-4000-8002-000000000305" concept="TypeRule.Fixed">
                  <children role="type">
                    <node id="d0000000-0000-4000-8002-000000000405" concept="TypeRule.Primitive" name="bool">
                      <properties>
                        <name>bool</name>
                      </properties>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </children>
          <children role="otherwise">
            <node id="d0000000-0000-4000-8002-000000000105" concept="TypeRule.Child" name="left">
              <properties>
                <slot>left</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>