<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="9b004c92-cc21-4202-82b4-2bf7fcbb3f41" name="Shop">
  <nodes>
    <node id="c1c8de03-35d1-4e9b-a38e-a5b1d5f58aaf" concept="Sql.Schema" name="shop">
      <properties>
        <name>shop</name>
      </properties>
      <children role="tables">
        <node id="1c97e5ba-eaea-44f5-bd42-8cf093ae3d23" concept="Sql.Table" name="person">
          <properties>
            <name>person</name>
          </properties>
          <children role="columns">
            <node id="8436ba5b-8248-45fa-9559-2bacd8e784a1" concept="Sql.Column" name="id">
              <properties>
                <name>id</name>
                <type>int</type>
              </properties>
            </node>
            <node id="cfb029ca-e0d0-4b16-8e53-ca6dcf3a54e6" concept="Sql.Column" name="name">
              <properties>
                <name>name</name>
                <type>text</type>
              </properties>
            </node>
            <node id="4ca011b2-b949-43f8-a50f-d0d6d0a3b031" concept="Sql.Column" name="city_id">
              <properties>
                <name>city_id</name>
                <type>int</type>
              </properties>
            </node>
          </children>
          <children role="keys">
            <node id="e0dc3f9c-f0dc-4e84-a792-f6abfb54e168" concept="Sql.ForeignKey">
              <references role="local" target="4ca011b2-b949-43f8-a50f-d0d6d0a3b031" resolveInfo="city_id" />
              <references role="target" target="30dc804c-9def-492e-9d78-ed98c59b2ceb" resolveInfo="id" />
            </node>
          </children>
        </node>
        <node id="d8f79166-799d-41d9-9bf4-9d87aa13c801" concept="Sql.Table" name="city">
          <properties>
            <name>city</name>
          </properties>
          <children role="columns">
            <node id="30dc804c-9def-492e-9d78-ed98c59b2ceb" concept="Sql.Column" name="id">
              <properties>
                <name>id</name>
                <type>int</type>
              </properties>
            </node>
            <node id="e29c064b-f071-43ce-ac9f-4c44cbd0ffdd" concept="Sql.Column" name="name">
              <properties>
                <name>name</name>
                <type>text</type>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>