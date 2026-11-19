<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="2a7fd72c-4ace-4511-951d-ca61aa52a917" name="Release">
  <nodes>
    <node id="8bb09f41-cc6f-4b83-a0a8-9248bfc2e5d3" concept="Sm.Machine" name="Release">
      <properties>
        <name>Release</name>
      </properties>
      <references role="initial" target="6189ab99-4526-47fe-ba55-e376aeb36604" resolveInfo="Idle" />
      <children role="states">
        <node id="6189ab99-4526-47fe-ba55-e376aeb36604" concept="Sm.State" name="Idle">
          <properties>
            <name>Idle</name>
            <isFinal>false</isFinal>
          </properties>
        </node>
        <node id="3f3f965f-3c9d-4d4d-b87c-703fee86fbbe" concept="Sm.State" name="Running">
          <properties>
            <name>Running</name>
            <isFinal>false</isFinal>
          </properties>
        </node>
        <node id="64ec7313-f471-41c2-9527-8be51f61edfb" concept="Sm.State" name="Done">
          <properties>
            <name>Done</name>
            <isFinal>true</isFinal>
          </properties>
        </node>
      </children>
      <children role="transitions">
        <node id="ef97fea5-e59e-4348-b675-474d11f6a960" concept="Sm.Transition">
          <properties>
            <event>start</event>
          </properties>
          <references role="source" target="6189ab99-4526-47fe-ba55-e376aeb36604" resolveInfo="Idle" />
          <references role="target" target="3f3f965f-3c9d-4d4d-b87c-703fee86fbbe" resolveInfo="Running" />
          <children role="guard">
            <node id="9685b869-a928-4d94-8107-6c8cab12cb13" concept="Expressions.Binary">
              <properties>
                <operator>&gt;</operator>
              </properties>
              <children role="left">
                <node id="76f64006-c83d-4bfc-8149-868c7381239c" concept="Expressions.Name" name="attempts">
                  <properties>
                    <name>attempts</name>
                  </properties>
                </node>
              </children>
              <children role="right">
                <node id="b5f90593-6ae5-4156-a0e7-d4d1ecc665d1" concept="Expressions.IntLiteral">
                  <properties>
                    <value>0</value>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="83251a98-38f7-4fa9-8b44-83cad922f847" concept="Sm.Transition">
          <properties>
            <event>finish</event>
          </properties>
          <references role="source" target="3f3f965f-3c9d-4d4d-b87c-703fee86fbbe" resolveInfo="Running" />
          <references role="target" target="64ec7313-f471-41c2-9527-8be51f61edfb" resolveInfo="Done" />
        </node>
      </children>
    </node>
  </nodes>
</model>