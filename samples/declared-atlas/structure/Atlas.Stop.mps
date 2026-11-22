<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b0000000-0000-4000-9000-000000000005" name="Atlas.Stop">
  <nodes>
    <node id="b0000000-0000-4000-8000-000000000005" concept="Structure.Concept" name="Atlas.Stop">
      <properties>
        <key>Atlas.Stop</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>false</isRoot>
        <alias></alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>One link of a route: the place it stops at.</description>
        <nameSlot></nameSlot>
      </properties>
      <children role="references">
        <node id="b0000000-0000-4000-8000-000000000401" concept="Structure.ReferenceDecl" name="to">
          <properties>
            <name>to</name>
            <targetConceptKey>Atlas.Place</targetConceptKey>
            <allowOwnSubtree>false</allowOwnSubtree>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>