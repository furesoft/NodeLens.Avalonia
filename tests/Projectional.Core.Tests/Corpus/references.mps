<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="fdb22b5c-7620-4acb-b64b-228e79ee8f85" name="References">
  <nodes>
    <node id="66b44c20-a810-4595-8327-afcf05a586eb" concept="Test.Rule" name="Derived">
      <properties>
        <name>Derived</name>
        <enabled>true</enabled>
      </properties>
      <references role="inherits" target="c7883e59-7e8a-498b-add4-48db97795af5" resolveInfo="Base" />
      <references role="self" target="66b44c20-a810-4595-8327-afcf05a586eb" resolveInfo="Derived" />
      <children role="extends">
        <node id="071212ae-1110-45ff-9bf4-e28e93d33012" concept="Test.Rule" name="Inline">
          <properties>
            <name>Inline</name>
            <enabled>false</enabled>
          </properties>
        </node>
      </children>
    </node>
    <node id="c7883e59-7e8a-498b-add4-48db97795af5" concept="Test.Rule" name="Base">
      <properties>
        <name>Base</name>
        <enabled>true</enabled>
      </properties>
    </node>
  </nodes>
</model>